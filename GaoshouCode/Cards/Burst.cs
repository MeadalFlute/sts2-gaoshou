using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 爆发：技能（普通）。耗 0 能量 1 星辉。
// 流转（颜色与上一张牌完全不同）：获得 1(2) 点能量。
// 奇迹（非回合开始抽牌进入手牌）：获得 1(2) 点星辉。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Burst : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    // 泛光：流转可触发或奇迹可触发时亮起。
    protected override bool ShouldGlowGoldInternal =>
        GaoshouFlowTracker.IsFlowReady(this) || MiracleCounter.IsMiracleReady(this);

    // 悬浮释义：奇迹。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    public Burst() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 流转：获得 1(2) 点能量。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await PlayerCmd.GainEnergy(DynamicVars.GetRequired<EnergyVar>("Energy").BaseValue, Owner);

        // 奇迹：获得 1(2) 点星辉。
        if (MiracleCounter.IsMiracleReady(this))
            await PlayerCmd.GainStars(DynamicVars.GetRequired<StarsVar>("Stars").BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        // 能量 1 -> 2、星辉 1 -> 2。
        // 能量固定 1（不再升级）；星辉 1 -> 2。
        DynamicVars.GetRequired<StarsVar>("Stars").UpgradeValueBy(1);
    }
}