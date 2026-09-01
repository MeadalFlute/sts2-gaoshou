using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 过期饮料：技能（无色衍生）。耗 0 能量 1 星辉。
// 查看抽牌堆顶 4 张牌，选择 1 张加入手牌，弃置剩余牌。消耗。
[RegisterCard(typeof(TokenCardPool))]
public sealed class ExpiredDrink : ModCardTemplate, Gaoshou.Keywords.IWasteCard
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        CardKeyword.Ethereal,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Cards(4),
    ];

    public ExpiredDrink() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 参考 Diceomancer「深思/研究(Research)」的写法：
        // 抽牌堆顶 N 张参与选卡（FromCombatPile + topCards.Contains 过滤），
        // 选中的入手，其余弃置。
        var pile = PileType.Draw.GetPile(Owner);
        var topCards = pile.Cards.Take(DynamicVars.Cards.IntValue).ToList();
        if (topCards.Count == 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(new LocString("cards", "GAOSHOU_EXPIRED_DRINK_PROMPT"), 1, 1);
        var selected = (await CardSelectCmd.FromCombatPile(choiceContext, pile, Owner, prefs, topCards.Contains)).ToList();
        foreach (var c in selected)
        {
            topCards.Remove(c);
        }

        if (selected.Count != 0)
        {
            await CardPileCmd.Add(selected, PileType.Hand);
        }
        await CardCmd.Discard(choiceContext, topCards);
    }

    protected override void OnUpgrade()
    {
        // 暂不升级：数值固定，升级可后续再设计。
    }
}