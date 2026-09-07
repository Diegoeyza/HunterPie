using HunterPie.Features.Statistics.Models;
using Newtonsoft.Json;
using System.Linq;

namespace HunterPie.Integrations.Poogie.Statistics.Models;

internal record PoogieGearStatusModel(
    [property: JsonProperty("raw")] double Raw,
    [property: JsonProperty("element")] double Element,
    [property: JsonProperty("affinity")] double Affinity
)
{
    public GearStatusModel ToEntity() => new GearStatusModel(
        Raw: Raw,
        Element: Element,
        Affinity: Affinity
    );

    public static PoogieGearStatusModel From(GearStatusModel model) =>
        new PoogieGearStatusModel(
            Raw: model.Raw,
            Element: model.Element,
            Affinity: model.Affinity
        );
}
