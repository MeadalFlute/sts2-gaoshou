using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 逸散：技能（稀有）。耗 1 能量 1 星辉（升级 0/1）。击晕一名敌人。消耗。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Negation : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Negation.png");

    // 悬浮释义：击晕。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(MegaCrit.Sts2.Core.HoverTips.StaticHoverTip.Stun),
    ];

    // 消耗：打出后移除。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public Negation() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 1 能量 1 星辉（升级 0 能量 1 星辉）。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.Stun(cardPlay.Target, null);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 能量 1 -> 0
    }
}
