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
// 参考 Diceomancer(骰子漫游者) 的 Phantom.cs 实现。
[RegisterSingleton]
public sealed class PhantomSingleton : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public PhantomSingleton()
    {
        ModHelper.SubscribeForCombatStateHooks(base.Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        var count = GetPhantomCount(card);
        if (count <= 0)
            return Task.CompletedTask;
        return TriggerPhantomEffects(choiceContext, card, count);
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

    private static async Task TriggerPhantomEffects(PlayerChoiceContext choiceContext, CardModel card, int count)
    {
        for (var i = 0; i < count; i++)
        {
            CardModel cardModel = card.CreateClone();
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
            // 幻影复制品不再携带"幻影次数"：否则该复制品再次被打出时会再次生成幻影（闪电打击实测问题）。
            if (cardModel.DynamicVars.TryGetValue("PhantomCount", out var phantomCountVar))
                phantomCountVar.BaseValue = 0;
            await CardPileCmd.AddGeneratedCardToCombat(cardModel, PileType.Hand, card.Owner);
        }
    }
}
