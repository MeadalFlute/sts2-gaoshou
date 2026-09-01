using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 损失规避：技能（罕见）。耗 2 能量（0 星辉）。
// 选择任意张手牌，变化为硬纸板（升级后为硬纸板+）。消耗。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class LossAversion : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/LossAversion.png");

    // 悬浮释义：硬纸板（升级后硬纸板+，变化）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Cardboard>(IsUpgraded),
        HoverTipFactory.Static(MegaCrit.Sts2.Core.HoverTips.StaticHoverTip.Transform),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public LossAversion() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 选择任意张手牌（0 ~ 手牌数）。
        var hand = PileType.Hand.GetPile(Owner)?.Cards.ToList() ?? [];
        if (hand.Count == 0)
            return;
        var chosen = await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(new LocString("cards", "GAOSHOU_LOSS_AVERSION_PROMPT"), 0, hand.Count),
            null, this);
        foreach (var card in chosen)
        {
            var cardboard = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<Cardboard>(), Owner);
            if (cardboard == null)
                continue;
            if (IsUpgraded)
                CardCmd.Upgrade(cardboard);
            await CardCmd.Transform(card, cardboard);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：变化为"硬纸板+"。
    }
}