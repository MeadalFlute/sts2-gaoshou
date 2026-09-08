using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 十三幺：技能（稀有）。耗 1 能量 0 星辉。
// 若手牌中至少有 9 张牌且名称各不相同：将所有手牌设为免费，并从左到右依次打出。
// 参考：静默猎手-子弹时间（手牌免费）+ 低语耳环（从左到右依次自动打出）。
// 升级后获得"保留"。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ThirteenOrphans : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    // 泛光：手牌中含有 >= 9 种不同卡牌时亮起（触发条件就绪）。
    protected override bool ShouldGlowGoldInternal => GetDistinctHandTypeCount() >= 9;

    // 手牌中（排除十三幺自身 this）不同卡牌类型的种数，供高亮与触发判定共用。
    // 排除 this：十三幺在手牌时若算上自己会让高亮偏高；打出后它会离开手牌，两处需保持一致。
    private int GetDistinctHandTypeCount()
    {
        return Owner?.PlayerCombatState?.Hand.Cards
            .Where(c => !ReferenceEquals(c, this))
            .Select(c => c.GetType()).Distinct().Count() ?? 0;
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public ThirteenOrphans() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认"无星辉费用"。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 条件：手牌中（排除十三幺自身）含有 >= 9 种不同卡牌。
        if (GetDistinctHandTypeCount() < 9)
            return;

        // 打出十三幺后，手牌只剩 9 张（手牌上限 10，十三幺自身已出发）。
        var hand = PileType.Hand.GetPile(Owner);
        var cards = hand.Cards.ToList();   // 快照：只处理打出十三幺时的这批手牌，不处理之后抽上来的新牌。
        if (cards.Count == 0)
            return;

        // 先依次把这批手牌移入结算区（Play 区），锁定它们、脱离手牌变动；
        // 再逐张 AutoPlay 结算。这样上一张结算时，其它牌已在 Play 区，
        // 不会被手牌变化/弃置/消耗/变化影响（快照引用保持有效）。
        using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
        {
            foreach (var card in cards)
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;

                if (card.Pile?.Type != PileType.Play)
                    await CardPileCmd.Add(card, PileType.Play);
            }

            // 全部移入结算区后再逐张结算。
            foreach (var card in cards)
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;

                await CardCmd.AutoPlay(choiceContext, card, null);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后获得"保留"（直接改实例词条）。
        AddKeyword(CardKeyword.Retain);
    }
}