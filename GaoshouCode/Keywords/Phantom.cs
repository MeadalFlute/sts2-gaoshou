using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

// 幻影：打出带「幻影」词条的卡后，创建一张费用-1、无幻影、带消耗的临时复制品加入手牌。
// 复制品加入手牌的时机保持在牌完全结算后（AfterCardPlayedLate，同原版「愤怒」）。
// 附魔继承：在打出前(BeforeCardPlayed)克隆一份附魔快照，复制时用该"本次打出前"的版本——
//   * 一次性附魔(活力/华彩等)本次打出才被消耗 → 复制品拿到"未触发"版；
//   * 若附魔在打出前就已失效/用尽 → 快照仍是失效态，复制品不会被错误地重新附魔。
[RegisterSingleton]
public sealed class PhantomSingleton : SingletonModel
{
    // 卡牌实例 -> 本次打出前的附魔快照。
    private readonly Dictionary<CardModel, EnchantmentModel> _prePlayEnchant = [];

    public override bool ShouldReceiveCombatHooks => true;

    public PhantomSingleton()
    {
        ModHelper.SubscribeForCombatStateHooks(base.Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // 打出前：若该卡带附魔，克隆快照供复制时使用（打出后原附魔会被消耗）。
        var card = cardPlay.Card;
        if (card.Enchantment is { } ench && GetPhantomCount(card) > 0)
            _prePlayEnchant[card] = (EnchantmentModel)ench.ClonePreservingMutability();
        else
            _prePlayEnchant.Remove(card);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        var count = GetPhantomCount(card);
        if (count <= 0)
            return;
        // 取打出前的附魔快照（打出后原附魔已变化，不能用 card.Enchantment）。
        _prePlayEnchant.TryGetValue(card, out var prePlayEnchant);
        _prePlayEnchant.Remove(card);
        await TriggerPhantomEffects(choiceContext, card, count, prePlayEnchant);
    }

    /// <summary>
    /// 幻影数量：默认带「幻影」词条的卡为 1；卡上定义 PhantomCount 动态变量时可指定多次（闪电打击等）。
    /// </summary>
    private static int GetPhantomCount(CardModel? card)
    {
        if (card == null)
            return 0;
        if (card.DynamicVars.TryGetValue("PhantomCount", out var v) && v.IntValue >= 1)
            return v.IntValue;
        return card.Keywords.Contains(GaoshouKeyword.Phantom) ? 1 : 0;
    }

    private static async Task TriggerPhantomEffects(PlayerChoiceContext choiceContext, CardModel card, int count,
        EnchantmentModel? prePlayEnchant)
    {
        for (var i = 0; i < count; i++)
        {
            CardModel cardModel = card.CreateClone();
            // 用"本次打出前"的附魔快照：复制品拿到未触发/原状态的一次性附魔（CreateClone 不拷贝附魔）。
            if (prePlayEnchant != null)
                cardModel.EnchantInternal((EnchantmentModel)prePlayEnchant.ClonePreservingMutability(),
                    prePlayEnchant.Amount);
            // 双色卡的幻影复制品：随机抽取一个主色（实例级登记，mana 颜色释义按此显示单色）。
            if (card.GetType().GetProperty("CardColor")?.GetValue(card) is GaoshouCardColor srcColor)
            {
                var primaries = PhantomColorRegistry.GetPrimaries(srcColor);
                PhantomColorRegistry.Assign(cardModel,
                    primaries.Count > 1 ? primaries[Random.Shared.Next(primaries.Count)] : srcColor);
            }
            cardModel.EnergyCost.AddThisCombat(-1);
            cardModel.AddKeyword(CardKeyword.Exhaust);
            cardModel.RemoveKeyword(GaoshouKeyword.Phantom);
            // 幻影复制品移除"回响"：否则打出后会从消耗牌堆反复回手（三尖两刃枪实测问题）。
            cardModel.RemoveKeyword(GaoshouKeyword.Echo);
            // 保留"流转"：复制品正常承载流转伤害效果（描述仅剩"流转：造成伤害"一行）；无回响故不回手。
            // 幻影复制品不再携带"幻影次数"：否则该复制品再次被打出时会再次生成幻影（闪电打击实测问题）。
            if (cardModel.DynamicVars.TryGetValue("PhantomCount", out var phantomCountVar))
                phantomCountVar.BaseValue = 0;
            await CardPileCmd.AddGeneratedCardToCombat(cardModel, PileType.Hand, card.Owner);
        }
    }
}
