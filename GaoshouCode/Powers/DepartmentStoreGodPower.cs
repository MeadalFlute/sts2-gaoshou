using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 百货战神（能力）：**每回合你（升级后前两次 / 未升级首次）打出【废品】牌：获得 1 层力量、1 层敏捷。**
//
// 【2026-10-05 重做】
//   * 触发牌张由"临时牌（DeckVersion == null）"改为"**废品牌**（标记接口 IWasteCard）"——
//     把可累积的属性成长交回废品体系（资源/线性输出仍可跨体系共享）。
//   * 收益由"临时力量 + 临时敏捷（回合末减半）"改为"**永久力量 + 永久敏捷**，各 1 层"，
//     但加了"每回合次数上限"作为刹车 ⇒ 从"每次打出都叠"变成"每回合定额兑现"。
//   * **层数上限 = 本能力层数**（施加它的卡牌变量：未升级 1、升级 2）⇒ 卡牌升级即"首次 → 前两次" ✓
//
// 次数判定**不看自有状态、只看战斗历史**（与「取回 Fetch」同口径）：
//   * 统计"本回合已**结算**的、自己打出的废品牌"条数，并**排除本次出牌**（对象同一性比较），
//     因此不依赖"AfterCardPlayed 时本次出牌是否已入历史"这种实现细节 ✓
//   * 历史是全网同步的 ⇒ 多人两端结论一致，不需要额外的回合重置钩子，也没有"跨战斗残留"问题 ✓
//   * 开销：每次打出废品牌扫一遍本场已结算出牌（每回合几次），可接受；
//     ⚠️ 千万不要把它挪到"每次被询问都扫"的位置（旧版 LimitedPlayRule 就这么踩过"越玩越卡"）✗
[RegisterPower]
public sealed class DepartmentStoreGodPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/deptgod.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/deptgod.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay?.Player;
        var self = Owner.Player;
        if (self == null || player == null || player.NetId != self.NetId)
            return;

        // 只认废品牌。
        if (cardPlay!.Card is not IWasteCard)
            return;

        var limit = (int)Amount;
        if (limit <= 0)
            return;

        // 本回合已结算的废品牌出牌数（不含本次）达到上限 ⇒ 不再兑现。
        if (CountWastePlaysThisTurn(player, cardPlay) >= limit)
            return;

        // 永久力量 + 永久敏捷各 1 层（固定 1；层数只作为"每回合次数"使用）。
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, 1m, Owner, cardPlay.Card);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, 1m, Owner, cardPlay.Card);
    }

    /// <summary>
    /// 本回合"已结算"的、属于该玩家的废品牌出牌数，**排除** <paramref name="current" /> 这一次。
    /// 用对象同一性排除本次，避免依赖 AfterCardPlayed 与历史入账的先后顺序。
    /// </summary>
    private static int CountWastePlaysThisTurn(Player player, CardPlay current)
    {
        var history = CombatManager.Instance?.History;
        var combatState = player.Creature?.CombatState;
        if (history == null || combatState == null)
            return 0;

        var count = 0;
        foreach (var entry in history.CardPlaysFinished)
        {
            var play = entry?.CardPlay;
            if (play == null || ReferenceEquals(play, current))
                continue;

            if (play.Player == null || play.Player.NetId != player.NetId)
                continue;

            if (play.Card is not IWasteCard)
                continue;

            if (!entry!.HappenedThisTurn(combatState))
                continue;

            count++;
        }

        return count;
    }
}
