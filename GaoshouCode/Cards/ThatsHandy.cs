using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 这个顺手：能力（罕见）。耗 0 能量 1 星辉（升级 0/0）。每当你打出【临时】牌后，对随机敌人造成 3（4）点伤害。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ThatsHandy : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/ThatsHandy.png");

    // 悬浮释义：临时（自定义词条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Temporary),
    ];

    public ThatsHandy() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    // 伤害 3（升级 4）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("PowerAmount", 3),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ThisHandyPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("PowerAmount").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);   // 星辉 1 -> 0
        DynamicVars.GetRequired<IntVar>("PowerAmount").UpgradeValueBy(1);   // 3 -> 4
    }
}
