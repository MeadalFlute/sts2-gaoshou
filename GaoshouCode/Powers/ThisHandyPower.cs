using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 这个顺手（能力）：每当你**生成**一张【临时】牌后，对随机敌人造成 Amount 点伤害。
//
// 【2026-10-05 重做】触发点由"**打出**临时牌后"改为"**生成**临时牌后"：
//   * 钩子用原版 `AfterCardGeneratedForCombat(card, creator)`（与「武器库 ArsenalPower」同一钩子，
//     见反编译 Models\Powers\ArsenalPower.cs:19-26）—— 凡是 `CardPileCmd.AddGeneratedCardToCombat`
//     的生成路径都会走到这里（本模组的废品牌/装填/喷射器等生成同样计入）✓
//   * 生成物一律是"不在卡组里"的战斗内牌 ⇒ 与词条【临时】的口径一致 ✓
//   * 只认**自己**生成的牌：生成者与牌主都必须是本能力拥有者（多人防串触发）✓
//   * 该钩子没有 choiceContext / cardPlay ⇒ 用 `ThrowingPlayerChoiceContext`（本模组既有一致做法）✓
[RegisterPower]
public sealed class ThisHandyPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/thishandy.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/thishandy.png");

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        var self = Owner.Player;
        if (self == null || creator == null || creator.NetId != self.NetId)
            return;

        // 生成物必须也属于自己（防队友/敌人那边的生成串到本能力上）。
        if (card?.Owner?.NetId != self.NetId)
            return;

        var enemies = Owner.CombatState?.HittableEnemies.ToList() ?? [];
        if (enemies.Count == 0)
            return;

        var enemy = self.RunState.Rng.CombatTargets.NextItem(enemies)!;
        Flash();
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), enemy, Amount, ValueProp.Unpowered, Owner, card, null);
    }
}
