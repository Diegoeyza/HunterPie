using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Enums;
using HunterPie.Features.Statistics.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterPie.Integrations.Poogie.Statistics.Models;

internal record PoogieMonsterStatisticsModel(
    [property: JsonProperty("id")] int Id,
    [property: JsonProperty("variant")] int? Variant,
    [property: JsonProperty("max_health")] float MaxHealth,
    [property: JsonProperty("crown")] Crown Crown,
    [property: JsonProperty("enrage")] PoogieMonsterStatusStatisticsModel Enrage,
    [property: JsonProperty("ailments")] PoogieMonsterAilmentStatisticsModel[]? Ailments,
    [property: JsonProperty("hunt_started_at")] DateTime? HuntStartedAt,
    [property: JsonProperty("hunt_finished_at")] DateTime? HuntFinishedAt,
    [property: JsonProperty("hunt_type")] MonsterHuntType? HuntType,
    [property: JsonProperty("health_steps")] List<PoogieMonsterHealthStepModel>? HealthSteps,
    [property: JsonProperty("is_target",
        NullValueHandling = NullValueHandling.Ignore)] bool? IsTarget = null
)
{
    public MonsterModel ToEntity() => new(
        Id: Id,
        MaxHealth: MaxHealth,
        Crown: Crown,
        Enrage: Enrage.ToEntity(),
        Ailments: Ailments?.ToDictionary(
            it => it.Id,
            it => it.ToEntity()
        ) ?? new Dictionary<string, MonsterStatusModel>(),
        HuntStartedAt: HuntStartedAt,
        HuntFinishedAt: HuntFinishedAt,
        HuntType: HuntType,
        Variant: (VariantType?)Variant ?? VariantType.Normal,
        HealthSteps: HealthSteps?.Select(it => it.ToEntity())
            .ToList() ?? new List<MonsterHealthStepModel>(),
        IsTarget: IsTarget
    );

    public static PoogieMonsterStatisticsModel From(MonsterModel model) =>
        new(
            Id: model.Id,
            MaxHealth: model.MaxHealth,
            Crown: model.Crown,
            Enrage: PoogieMonsterStatusStatisticsModel.From(model.Enrage),
            Ailments: model.Ailments.Select(PoogieMonsterAilmentStatisticsModel.From).ToArray(),
            HuntStartedAt: model.HuntStartedAt,
            HuntFinishedAt: model.HuntFinishedAt,
            HuntType: model.HuntType,
            Variant: (int)model.Variant,
            HealthSteps: model.HealthSteps.Select(PoogieMonsterHealthStepModel.From).ToList(),
            IsTarget: model.IsTarget
        );
}