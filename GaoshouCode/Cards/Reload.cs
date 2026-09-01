using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 装弹：状态（无色衍生）。耗 1 能量 1 星辉。打出后把自己变化回双持冲锋枪。
[RegisterCard(typeof(TokenCardPool))]
public sealed class Reload : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Status;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Reload.png");

    // 悬浮释义：双持冲锋枪（变化目标卡）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<DualSMG>(),
    ];

    public override int CanonicalStarCost => 1;

    public Reload() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 将一张双持冲锋枪加入你的手牌（本卡照常进消耗）。
        var smg = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<DualSMG>(), Owner);
        if (smg != null)
            await CardPileCmd.AddGeneratedCardToCombat(smg, PileType.Hand, Owner);
    }
}