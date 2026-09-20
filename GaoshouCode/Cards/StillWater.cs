using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using Gaoshou.Powers;

namespace Gaoshou.Cards;

// 止水：技能（普通）。耗 0 能量 1 辉星。获得 1 点临时力量；增幅2。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class StillWater : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：增幅（词条）、临时力量（能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Amplify),
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("AmplifyCount", 2),
        ModCardVars.Int("Strength", 1),
    ];

    public StillWater() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 辉星（升级 0 能量 0 辉星）。
    public override int CanonicalStarCost => 1;

    // 主效果：获得 1 层临时力量（同步等量力量）。
    private async Task MainOnceAsync(PlayerChoiceContext choiceContext)
    {
        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("Strength").BaseValue, Owner.Creature, this);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await MainOnceAsync(choiceContext);

        // 增幅1：弃置最多 1 张牌，然后按实际弃置数重放。
        // 增幅：弃置最多 1(2) 张牌后重放主效果。
        var amplifyCount = (int)DynamicVars.GetRequired<IntVar>("AmplifyCount").BaseValue;
        await this.AmplifyAsync(choiceContext, amplifyCount, MainOnceAsync);
    }

    protected override void OnUpgrade()
    {
        // 升级：辉星→0。
        DynamicVars.GetRequired<IntVar>("AmplifyCount").UpgradeValueBy(2);   // 2 -> 4
        // UpgradeStarCostBy(-1);
    }
}
