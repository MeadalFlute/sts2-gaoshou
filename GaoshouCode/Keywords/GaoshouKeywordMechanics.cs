using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
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
        var discarded = (await CardSelectCmd.FromHand(choiceContext, card.Owner, prefs, null, card)).ToList();
        foreach (var c in discarded)
            await CardCmd.Discard(choiceContext, c);

        for (var i = 0; i < discarded.Count; i++)
            await mainOnce(choiceContext);
    }
}