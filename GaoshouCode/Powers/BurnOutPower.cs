using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 燃尽（能力）：每回合结束时，失去 1 点力量（不可格挡、不受力量）。
[RegisterPower]
public sealed class BurnOutPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/burnout.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/burnout.png");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
            return;
        if (!participants.Contains(Owner))
            return;

        // 每回合结束时，失去与层数等量的力量（多个燃尽 buff 叠加：Amount 累加 → 扣 Amount 层）。
        var strength = Owner.Powers.OfType<StrengthPower>().FirstOrDefault();
        // if (strength?.Amount > 0)
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, -Amount, Owner, null);
    }
}