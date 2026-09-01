using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Keywords;

// 词条机制共享助手。幻影（临时复制品）+ 增幅（弃置最多 n 张后重放 x 次）。
public static class GaoshouKeywordMechanics
{
    // 幻影：把 this 卡的临时复制品加入手牌（去掉幻影自身、带消耗）。
    public static async Task AddPhantomCopyAsync(this ModCardTemplate card, PlayerChoiceContext choiceContext)
    {
        var owner = card.Owner;
        var combat = owner.Creature.CombatState;
        if (combat == null)
            return;

        var ph = combat.CloneCard(card);
        if (ph == null)
            return;

        // 双色卡的幻影复制品：随机抽取其中一个主色（实例级覆盖 mana 颜色展示）。
        if (card.GetType().GetProperty("CardColor")?.GetValue(card) is GaoshouCardColor srcColor)
        {
            var primaries = PhantomColorRegistry.GetPrimaries(srcColor);
            if (primaries.Count > 1)
                PhantomColorRegistry.Assign(ph, primaries[Random.Shared.Next(primaries.Count)]);
            else
                PhantomColorRegistry.Assign(ph, srcColor);
        }

        // TODO(费用-1): 幻影应为"费用-1"。用 TryModifyEnergyCostInCombat / CardEnergyCost 把能量费用降 1。
        ph.RemoveKeyword(GaoshouKeyword.Phantom);
        ph.RemoveKeyword(GaoshouKeyword.Echo);   // 移除回响，避免消耗复制品从消耗牌堆反复回手。
        ph.AddKeyword(CardKeyword.Exhaust);
        // 幻影复制品是局内生成的临时牌（不属于牌组），火花/百货战神/这个顺手等
        // 通过 card.DeckVersion == null 直接鉴定，无需额外词条。

        var result = await CardPileCmd.AddGeneratedCardToCombat(ph, PileType.Hand, owner);
        if (result.success)
            ph.Pile?.InvokeCardAddFinished();
    }

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