using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 借用之牌：技能（稀有）。耗 1 能量。选择手牌中至多 2(3) 张牌，将它们的一张临时复制品加入你的手牌。消耗、虚无。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class BorrowedCards : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        CardKeyword.Ethereal,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("CopyCount", 2),
    ];

    public BorrowedCards() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var max = (int)DynamicVars.GetRequired<IntVar>("CopyCount").BaseValue;
        var hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        // 选择至多 max 张手牌（可少选）。
        var prefs = new CardSelectorPrefs(new LocString("cards", "GAOSHOU_BORROWED_CARDS_PROMPT"), 0, Math.Min(max, hand.Count));
        var selected = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, null, this)).ToList();

        // 各自生成一张临时复制品加入手牌（幻影同款：CreateClone + AddGeneratedCardToCombat）。
        foreach (var c in selected)
        {
            var clone = c.CreateClone();
            await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("CopyCount").UpgradeValueBy(1);   // 2 -> 3
    }
}