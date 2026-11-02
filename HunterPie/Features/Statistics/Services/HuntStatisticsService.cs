using HunterPie.Core.Client.Configuration.Enums;
using HunterPie.Core.Crypto;
using HunterPie.Core.Extensions;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Enums;
using HunterPie.Features.Statistics.Models;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;
using HunterPie.Integrations.Datasources.MonsterHunterWilds.Entity.Game;
using HunterPie.Integrations.Datasources.MonsterHunterWorld.Entity.Game;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterPie.Features.Statistics.Services;

internal class HuntStatisticsService : IHuntStatisticsService<HuntStatisticsModel>
{
    // A non-target monster must lose more than 60% HP to count as fought.
    private const double EngagementHpFloor = 0.40;

    private readonly IContext _context;
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly List<IHuntStatisticsService<MonsterModel>> _monsterStatisticsServices = new();
    private readonly List<IHuntStatisticsService<PartyMemberModel>> _partyMembersStatisticsServices = new();

    public HuntStatisticsService(IContext context)
    {
        _context = context;

        HookEvents();
    }

    public HuntStatisticsModel Export() =>
        Export(
            _context.Game.Quest?.Status ?? QuestStatus.None,
            includeEngagedMonsters: false
        );

    /// <summary>
    /// Exports the current hunt. <paramref name="status"/> is passed explicitly
    /// because the quest object's own status can still be stale at quest end.
    /// Failed attempts pass <paramref name="includeEngagedMonsters"/> so a
    /// monster that was damaged (lost at least 1% HP) but never enraged is
    /// still recorded. A monster with only a trace HP change is a bystander.
    /// </summary>
    public HuntStatisticsModel Export(QuestStatus status, bool includeEngagedMonsters)
    {
        QuestDetailsModel? questModel = _context.Game.Quest switch
        {
            { } quest => new QuestDetailsModel(
                Id: quest.Id,
                Type: quest.Type,
                Deaths: quest.Deaths,
                MaxDeaths: quest.MaxDeaths,
                Level: quest.Level,
                Stars: quest.Stars,
                Cleared: status switch
                {
                    QuestStatus.Success => true,
                    QuestStatus.Fail or QuestStatus.Quit => false,
                    _ => null
                }
            ),
            _ => null
        };

        var players = _partyMembersStatisticsServices.Select(s => s.Export())
            .ToList();

        var exportedMonsters = _monsterStatisticsServices.Select(s => s.Export())
            .ToList();
        bool hasTargets = exportedMonsters.Any(it => it.IsTarget == true);

        var monsters = exportedMonsters.Where(it =>
        {
            // Quest targets are always relevant, including speedruns that kill
            // before the first enrage/ailment.
            if (it.IsTarget == true)
                return true;

            // Non-targets must have lost more than 60% HP. This keeps
            // turf-war bystanders out while still recording a monster the
            // party actually fought.
            if (hasTargets)
                return it.HealthSteps.Any(step =>
                    step.Percentage < EngagementHpFloor);

            // No target identity available (old dump/expedition): fall back to
            // the previous heuristics. Successful legacy hunts also accept
            // slain/captured monsters, which covers speedruns without enrage
            // or ailments.
            return it.Enrage.Activations.Length > 0
                   || it.Ailments.Count > 0
                   || (status == QuestStatus.Success
                       && (it.HuntType is MonsterHuntType.Slay or MonsterHuntType.Capture
                           || it.HealthSteps.Any(step =>
                               step.Percentage < EngagementHpFloor)))
                   || (includeEngagedMonsters
                       && it.HealthSteps.Any(step => step.Percentage <= 0.99));
        }).ToList();

        return new HuntStatisticsModel(
            Game: GetGameType(),
            Quest: questModel,
            Players: players,
            Monsters: monsters,
            StartedAt: _startedAt,
            FinishedAt: DateTime.UtcNow,
            UploadedAt: DateTime.UtcNow,
            Hash: GenerateHuntHash(players, monsters)
        );
    }

    private void HookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin += OnPartyMemberJoin;
        _context.Game.OnMonsterSpawn += OnMonsterSpawn;


        foreach (IPartyMember player in _context.Game.Player.Party.Members)
            HandlePartyMember(player);

        foreach (IMonster monster in _context.Game.Monsters)
            HandleMonster(monster);
    }

    private void UnhookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin -= OnPartyMemberJoin;
        _context.Game.OnMonsterSpawn -= OnMonsterSpawn;
    }

    private void OnMonsterSpawn(object? sender, IMonster e) => HandleMonster(e);

    private void OnPartyMemberJoin(object? sender, IPartyMember e) => HandlePartyMember(e);

    private void HandleMonster(IMonster e)
    {
        _monsterStatisticsServices.Add(new MonsterStatisticsService(_context, e));
    }

    private void HandlePartyMember(IPartyMember member)
    {
        if (member is not { Type: MemberType.Player or MemberType.Companion })
            return;

        _partyMembersStatisticsServices.Add(new PartyMemberStatisticsService(_context, member));
    }

    private static string GenerateHuntHash(List<PartyMemberModel> players, List<MonsterModel> monsters)
    {
        string[] data = players.Select(p => p.Name)
            .Concat(monsters.Select(m => m.Id.ToString()))
            .Where(e => !string.IsNullOrEmpty(e))
            .OrderBy(e => e.Length)
            .ToArray();

        string uniqueString = string.Join('|', data);

        return HashService.Hash(uniqueString);
    }

    public void Dispose()
    {
        UnhookEvents();

        _monsterStatisticsServices.DisposeAll();
        _monsterStatisticsServices.Clear();

        _partyMembersStatisticsServices.DisposeAll();
        _partyMembersStatisticsServices.Clear();
    }

    private GameType GetGameType()
    {
        return _context.Game switch
        {
            MHRGame => GameType.Rise,
            MHWGame => GameType.World,
            MHWildsGame => GameType.Wilds,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}