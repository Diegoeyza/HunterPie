using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Events;
using HunterPie.Core.Observability.Logging;
using HunterPie.Domain.Interfaces;
using HunterPie.Features.Statistics.Models;
using HunterPie.Integrations.Poogie.Statistics.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HunterPie.Features.Statistics.Services;

/// <summary>
/// Analytics-fork addition (not upstream): mirrors <see cref="QuestTrackerService"/>
/// but writes the quest-end payload to a local JSON file instead of uploading it.
/// No account, no network — one file per successful hunt, safe to re-import.
/// </summary>
internal class HuntFileDumpService : IContextInitializer, IDisposable
{
    private readonly ILogger _logger = LoggerFactory.Create();

    private IContext? _context;
    private HuntStatisticsService? _statisticsService;

    private static string DumpDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HuntExports");

    private void HookEvents()
    {
        if (_context is null)
            return;

        _context.Game.OnQuestStart += OnQuestStart;
        _context.Game.OnQuestEnd += OnQuestEnd;
    }

    private void UnhookEvents()
    {
        if (_context is null)
            return;

        _context.Game.OnQuestStart -= OnQuestStart;
        _context.Game.OnQuestEnd -= OnQuestEnd;
    }

    private void OnQuestStart(object? sender, IQuest e)
    {
        if (_context is null)
            return;

        bool shouldIgnore = e.Type switch
        {
            QuestType.Hunt
                or QuestType.Slay
                or QuestType.Capture
                or QuestType.Special => false,
            _ => true
        };

        if (shouldIgnore)
            return;

        _statisticsService?.Dispose();
        _statisticsService = new HuntStatisticsService(_context);
    }

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
    {
        if (_statisticsService is null)
            return;

        try
        {
            HuntStatisticsModel exported = _statisticsService.Export();

            if (e.Status != QuestStatus.Success || exported.Monsters.Count == 0)
            {
                _logger.Debug($"Hunt not dumped (status: {e.Status}, monsters: {exported.Monsters.Count})");
                return;
            }

            DateTime finishedAt = exported.StartedAt.Add(e.TimeElapsed);
            exported = exported with { FinishedAt = finishedAt };

            var payload = PoogieQuestStatisticsModel.From(exported);

            Directory.CreateDirectory(DumpDirectory);
            string fileName = $"{exported.Hash}_{finishedAt:yyyyMMdd_HHmmss}.json";
            string path = Path.Combine(DumpDirectory, fileName);

            File.WriteAllText(path, JsonConvert.SerializeObject(payload, Formatting.Indented));
            _logger.Debug($"Hunt dumped to {path}");
        }
        catch (Exception ex)
        {
            // Dumping must never break the overlay: log and continue.
            _logger.Error($"Failed to dump hunt to {DumpDirectory}\n{ex}");
        }
        finally
        {
            _statisticsService?.Dispose();
            _statisticsService = null;
        }
    }

    public void Dispose()
    {
        UnhookEvents();
        _statisticsService?.Dispose();
        _statisticsService = null;
    }

    public Task InitializeAsync(IContext context)
    {
        _context = context;
        HookEvents();

        return Task.CompletedTask;
    }
}
