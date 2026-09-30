using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Gaoshou.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Keywords;

// 词条机制共享助手。增幅：弃置最多 n 张后重放 x 次。
//
// 幻影（临时复制品）的实现**不在这里**：早先的 AddPhantomCopyAsync 已被 PhantomSingleton
// （Keywords\Phantom.cs，即 1.1.4 里的 PhantomSingleton.cs）取代，且全仓库无调用者，故删除。
// 幻影复制品的单色分配统一在 PhantomSingleton 里用**同步 RNG**完成
// （card.Owner.RunState.Rng.CombatCardGeneration），不要再用本地 Random.Shared。
public static class GaoshouKeywordMechanics
{
    /// <summary>
    /// 增幅：弃置手牌中最多 <paramref name="maxDiscard" /> 张，然后按实际弃置数重放本卡效果
    /// （通过 <paramref name="mainOnce" /> 重放主效果；纯代码重放，多人两端一致，避免 BaseReplayCount 反序列化分歧）。
    /// </summary>
    public static async Task AmplifyAsync(
        this ModCardTemplate card,
        PlayerChoiceContext choiceContext,
        int maxDiscard,
        Func<PlayerChoiceContext, Task> mainOnce)
    {
        if (maxDiscard <= 0)
            return;

        var hand = PileType.Hand.GetPile(card.Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        var prompt = new LocString("cards", "GAOSHOU_AMPLIFY_PROMPT");
        prompt.Add("MaxCount", maxDiscard);
        var prefs = new CardSelectorPrefs(prompt, 0, Math.Min(maxDiscard, hand.Count));
        // 复刻原版 FromHandForDiscard 的临时高光规则：只有真正的奇巧牌可亮。
        // 呼吸不是奇巧牌，但其弃置效果会在此处触发，因此额外显示同款提示。
        prefs.ShouldGlowGold = candidate =>
        {
            if (candidate is Breath)
                return true;
            if (!candidate.IsSlyThisTurn)
                return false;

            return candidate.CanPlay(out var reason, out _) || reason.HasResourceCostReason();
        };
        var discarded = (await CardSelectCmd.FromHand(choiceContext, card.Owner, prefs, null, card)).ToList();
        foreach (var c in discarded)
            await CardCmd.Discard(choiceContext, c);

        for (var i = 0; i < discarded.Count; i++)
            await mainOnce(choiceContext);
    }

    /// <summary>
    /// 高光是否允许出现在当前界面：只允许出现在"正常手牌行"里。
    /// 手牌选择模式（弃牌 / 选牌界面）下卡牌**仍然留在手牌堆里**（<c>PileType.Hand</c>），
    /// 单靠牌堆判定挡不住；且原版此时会用 <c>CardSelectorPrefs.ShouldGlowGold</c>
    /// （经 <c>NPlayerHand.SelectModeGoldGlowOverride</c>）接管泛光判定 ⇒ 模组必须自己让位，
    /// 否则改色后的流转/奇迹/风暴高光会照到选牌界面上。
    /// 房间/手牌节点还没建好时（<c>NCombatRoom.Instance</c> 为空）按"允许"处理，行为与旧版一致。
    /// </summary>
    public static bool IsHandGlowAllowed()
    {
        var hand = NCombatRoom.Instance?.Ui?.Hand;
        return hand == null || !hand.IsInCardSelection;
    }
}
