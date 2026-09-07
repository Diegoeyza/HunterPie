using HunterPie.Core.Game.Enums;

namespace HunterPie.Features.Statistics.Models;

internal record PartyMemberModel(
    string Name,
    Weapon Weapon,
    GearStatusModel? Gear,
    PlayerDamageFrameModel[] Damages,
    AbnormalityModel[] Abnormalities,
    bool IsHunterPieUser
);