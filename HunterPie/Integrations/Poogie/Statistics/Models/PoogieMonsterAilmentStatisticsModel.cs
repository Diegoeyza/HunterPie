using HunterPie.Features.Statistics.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace HunterPie.Integrations.Poogie.Statistics.Models;

internal record PoogieMonsterAilmentStatisticsModel(
    [property: JsonProperty("id")] string Id,
    [property: JsonProperty("activations")] PoogieTimeFrameModel[] Activations
)
{
    public MonsterStatusModel ToEntity() =>
        new(
            Activations: Activations.Select(it => it.ToEntity()).ToArray()
        );

    public static PoogieMonsterAilmentStatisticsModel From(
        KeyValuePair<string, MonsterStatusModel> model
    ) =>
        new(
            Id: model.Key,
            Activations: model.Value.Activations.Select(PoogieTimeFrameModel.From).ToArray()
        );
}
