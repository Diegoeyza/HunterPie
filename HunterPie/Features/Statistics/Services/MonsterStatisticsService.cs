using HunterPie.Core.Extensions;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Enums;
using HunterPie.Features.Statistics.Models;
using System;
using System.Collections.Generic;

namespace HunterPie.Features.Statistics.Services;

internal class MonsterStatisticsService : IHuntStatisticsService<MonsterModel>
{
    private const double RECORD_HEALTH_STEP = 0.05;

    private readonly IContext _context;
    private readonly IMonster _monster;

    private int _monsterId;
    private readonly VariantType _variant;
    private float? _startHealth;
    private readonly Stack<TimeFrameModel> _enrages = new();
    private readonly Dictionary<string, Stack<TimeFrameModel>> _ailments = new();
    private readonly Dictionary<IMonsterAilment, bool> _ailmentStates = new();
    private readonly Dictionary<string, int> _activeAilments = new();
    private readonly HashSet<IMonsterAilment> _hookedAilments = new();
    private readonly List<MonsterHealthStepModel> _healthSteps = new();

    private DateTime? _huntStart;
    private DateTime? _huntEnd;
    private float _maxHealth;
    private Crown _crown;
    private MonsterHuntType _huntType;
    private float _lastPercentageRecorded = 2.0f;

    public MonsterStatisticsService(IContext context, IMonster monster)
    {
        _context = context;
        _monster = monster;
        _variant = monster.Variant;
        _monsterId = _monster.Id;
        _maxHealth = monster.MaxHealth;
        _crown = monster.Crown;

        HookEvents();

        foreach (IMonsterAilment ailment in monster.Ailments)
            HookAilment(ailment);
    }

    public MonsterModel Export()
    {
        TimeFrameModel? lastEnrage = _enrages.PopOrDefault();

        if (_huntStart is { })
            _enrages.PushNotNull(
                lastEnrage?.IsRunning() == true
                    ? lastEnrage.End()
                    : lastEnrage
                );

        _huntEnd = _huntStart is { } ? DateTime.UtcNow : null;

        return new MonsterModel(
            Id: _monsterId,
            MaxHealth: _maxHealth,
            Crown: _crown,
            Enrage: new MonsterStatusModel(
                Activations: _enrages.ToArray()
            ),
            Ailments: BuildAilments(),
            HuntStartedAt: _huntStart,
            HuntFinishedAt: _huntEnd,
            HuntType: _huntType,
            Variant: _variant,
            HealthSteps: _healthSteps
        );
    }

    private void HookEvents()
    {
        _monster.OnSpawn += OnMonsterSpawn;
        _monster.OnEnrageStateChange += OnEnrageStateChange;
        _monster.OnNewAilmentFound += OnNewAilmentFound;
        _monster.OnCapture += OnCapture;
        _monster.OnDeath += OnDeath;
        _monster.OnCrownChange += OnCrownChange;
        _monster.OnHealthChange += OnHealthChange;
    }

    private void UnhookEvents()
    {
        _monster.OnSpawn -= OnMonsterSpawn;
        _monster.OnEnrageStateChange -= OnEnrageStateChange;
        _monster.OnNewAilmentFound -= OnNewAilmentFound;
        _monster.OnCapture -= OnCapture;
        _monster.OnDeath -= OnDeath;
        _monster.OnCrownChange -= OnCrownChange;
        _monster.OnHealthChange -= OnHealthChange;

        foreach (IMonsterAilment ailment in _hookedAilments)
            ailment.OnTimerUpdate -= OnAilmentTimerUpdate;
        _hookedAilments.Clear();
    }

    private void OnMonsterSpawn(object? sender, EventArgs e)
    {
        _monsterId = _monster.Id;
    }

    private void OnHealthChange(object? sender, EventArgs e)
    {
        _maxHealth = Math.Max(_maxHealth, _monster.MaxHealth);

        if (Math.Abs(_monster.Health - _monster.MaxHealth) < 0.1)
            return;

        float currentPercentage = Math.Max(_monster.Health / _monster.MaxHealth, 0.0f);
        if ((_lastPercentageRecorded - currentPercentage) >= RECORD_HEALTH_STEP)
        {
            _healthSteps.Add(
                item: new MonsterHealthStepModel(
                    Percentage: currentPercentage,
                    Time: DateTime.UtcNow
                )
            );
            _lastPercentageRecorded = currentPercentage;
        }

        if (_huntStart is not null || _startHealth is not null)
            return;

        _huntStart = DateTime.UtcNow;
        _startHealth = _monster.Health;
    }

    private void OnCrownChange(object? sender, EventArgs e)
    {
        _crown = _monster.Crown;
    }

    private void OnDeath(object? sender, EventArgs e)
    {
        _huntEnd = DateTime.UtcNow;
        _huntType = MonsterHuntType.Slay;
    }

    private void OnCapture(object? sender, EventArgs e)
    {
        _huntEnd = DateTime.UtcNow;
        _huntType = MonsterHuntType.Capture;
    }

    private void OnNewAilmentFound(object? sender, IMonsterAilment e) => HookAilment(e);

    /// <summary>
    /// Starts tracking one ailment slot. The game keeps separate slots for the
    /// same status (e.g. two poison ids), so windows are keyed by the status
    /// id and only the first active slot opens a window.
    /// </summary>
    private void HookAilment(IMonsterAilment ailment)
    {
        if (!_hookedAilments.Add(ailment))
            return;

        if (!IsTrackedAilment(ailment.Id))
            return;

        ailment.OnTimerUpdate += OnAilmentTimerUpdate;
        _ailmentStates[ailment] = false;
        UpdateAilmentState(ailment, isActive: ailment.Timer > 0);
    }

    private void OnAilmentTimerUpdate(object? sender, IMonsterAilment e)
        => UpdateAilmentState(e, isActive: e.Timer > 0);

    private void UpdateAilmentState(IMonsterAilment ailment, bool isActive)
    {
        bool wasActive = _ailmentStates.TryGetValue(ailment, out bool state) && state;
        if (wasActive == isActive)
            return;

        _ailmentStates[ailment] = isActive;
        string id = ailment.Id;

        if (!_ailments.TryGetValue(id, out Stack<TimeFrameModel>? activations))
        {
            activations = new Stack<TimeFrameModel>();
            _ailments[id] = activations;
        }

        int active = _activeAilments.GetValueOrDefault(id);

        if (isActive)
        {
            _activeAilments[id] = active + 1;

            if (active == 0)
            {
                // A slot first seen mid-status (e.g. hooked after it was
                // applied): backdate the window by its elapsed timer.
                DateTime startedAt = DateTime.UtcNow;
                double elapsed = Math.Max(0, ailment.MaxTimer - ailment.Timer);
                activations.Push(TimeFrameModel.Start(startedAt - TimeSpan.FromSeconds(elapsed)));
            }

            return;
        }

        _activeAilments[id] = Math.Max(0, active - 1);

        if (active <= 1)
        {
            TimeFrameModel? last = activations.PopOrDefault();
            activations.PushNotNull(last?.End());
        }
    }

    private static bool IsTrackedAilment(string id) =>
        id.StartsWith("AILMENT_", StringComparison.Ordinal)
        && id is not ("AILMENT_ENRAGE" or "AILMENT_UNKNOWN");

    private Dictionary<string, MonsterStatusModel> BuildAilments()
    {
        var exported = new Dictionary<string, MonsterStatusModel>();

        foreach ((string id, Stack<TimeFrameModel> activations) in _ailments)
        {
            TimeFrameModel? last = activations.PopOrDefault();

            if (last?.IsRunning() == true)
                last = last.End();

            activations.PushNotNull(last);

            if (activations.Count > 0)
                exported[id] = new MonsterStatusModel(activations.ToArray());
        }

        return exported;
    }

    private void OnEnrageStateChange(object? sender, EventArgs e)
    {
        if (_huntStart is not { })
            return;

        TimeFrameModel? lastEnrage = _enrages.PopOrDefault();

        _enrages.PushNotNull(lastEnrage?.End());

        if (!_monster.IsEnraged)
            return;

        _enrages.Push(TimeFrameModel.Start());
    }

    public void Dispose() => UnhookEvents();
}