using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Enums;
using System;
using System.Collections.Generic;

namespace HunterPie.Features.Statistics.Models;

internal record MonsterModel(
    int Id,
    float MaxHealth,
    Crown Crown,
    VariantType Variant,
    MonsterStatusModel Enrage,
    Dictionary<string, MonsterStatusModel> Ailments,
    DateTime? HuntStartedAt,
    DateTime? HuntFinishedAt,
    MonsterHuntType? HuntType,
    List<MonsterHealthStepModel> HealthSteps
);