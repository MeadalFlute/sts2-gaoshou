using Gaoshou.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers.Models;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

public sealed class WheelchairEnergyCostColorPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_wheelchair_energy_cost_color";
    public static string Description => "show unaffordable star-paid energy costs in red";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CardCostHelper), nameof(CardCostHelper.GetEnergyCostColor)),
    ];

    public static void Postfix(CardModel card, ICombatState? state, ref CardCostColor __result)
    {
        if (state == null || card.Owner?.Creature.GetPower<WheelchairStarPayPower>() == null)
            return;

        var resources = card.Owner.PlayerCombatState;
        if (resources == null || card.EnergyCost.CostsX)
            return;

        // Star payment reports only StarCostTooHigh, even when it is paying
        // an energy deficit. Reflect that failure on the energy label as well.
        if (card.EnergyCost.GetWithModifiers(CostModifiers.All) > resources.Energy
            && !resources.HasEnoughResourcesFor(card, out var reason)
            && reason.HasFlag(UnplayableReason.StarCostTooHigh))
        {
            __result = CardCostColor.InsufficientResources;
        }
    }
}
