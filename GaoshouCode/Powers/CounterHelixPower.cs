using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 反击螺旋（能力）：结构照原版「火焰屏障」FlameBarrierPower。
//   * 层数 Amount = **每次反击的伤害**（卡牌打出时把 5(8) 传进来）→ 图标上显示的就是它。
//   * 本回合每当你被"真正的攻击"命中（有攻击者 + props.IsPoweredAttack()）→ **立刻**对所有敌人反击一次。
//   * 伤害是 buff 伤害：ValueProp.Unpowered（不吃力量、也不触发荆棘等"被攻击"类敌方能力），
//     dealer 传玩家本人（同原版火焰屏障）。
//   * "本回合" = 敌方回合结束时移除（同原版火焰屏障的 AfterSideTurnEnd：Owner.Side != side）。
//
// ⚠️ AOE 的关键：**一次** CreatureCmd.Damage 传入整个敌人列表，而不是 for 循环逐个打 ——
// 逐个打会变成"依次结算"（每个敌人一段独立伤害），原版多目标伤害是同一批一起结算的。
[RegisterPower]
public sealed class CounterHelixPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/counterhelix.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/counterhelix.png");

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature? target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 与原版火焰屏障同口径：只对真正的"攻击"（有攻击者 + 被强化过的攻击），且攻击者必须是敌方。
        if (target != Owner || dealer == null || dealer.Side == Owner.Side || !props.IsPoweredAttack())
            return;

        var combatState = Owner.CombatState;
        if (combatState == null)
            return;

        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0)
            return;

        await CreatureCmd.Damage(
            choiceContext: choiceContext,
            targets: enemies,
            amount: Amount,
            props: ValueProp.Unpowered,
            dealer: Owner,
            cardSource: null,
            cardPlay: null);
    }

    // "本回合"：敌方回合结束时移除。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side)
            await PowerCmd.Remove(this);
    }
}
