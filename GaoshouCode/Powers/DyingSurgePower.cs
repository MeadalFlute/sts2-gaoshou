using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 搏命（能力）：本回合你造成的伤害翻倍；你的回合开始时，你直接死亡。
[RegisterPower]
public sealed class DyingSurgePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/dyingsurge.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/dyingsurge.png");

    // 本回合造成的伤害翻倍。
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner != dealer)
            return 1m;
        return 2m;
    }

    // 下回合（自己的回合）开始时：直接死亡。
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        return KillIfOwnTurn(side, participants);
    }

    private Task KillIfOwnTurn(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return Task.CompletedTask;
        return CreatureCmd.Kill(Owner);
    }
}