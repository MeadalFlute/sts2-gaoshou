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

// 止水：技能（普通）。耗 0 能量 1 星辉（升级 0/0）。获得 2 点临时力量（简化为 2 点力量）；增幅1（暂不实装）。
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
        ModCardVars.Int("AmplifyCount", 1),
        ModCardVars.Int("Strength", 2),
    ];

    public StillWater() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 星辉（升级 0 能量 0 星辉）。
    public override int CanonicalStarCost => 1;

    // 主效果：获得 2 层临时力量（同步等量力量）。
    private async Task MainOnceAsync(PlayerChoiceContext choiceContext)
    {
        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await MainOnceAsync(choiceContext);

        // 增幅1：弃置最多 1 张牌，然后按实际弃置数重放。
        // 增幅：弃置最多 1(3) 张牌后重放主效果。
        await this.AmplifyAsync(choiceContext, IsUpgraded ? 3 : 1, MainOnceAsync);
    }

    protected override void OnUpgrade()
    {
        // 升级：增幅 1 -> 3（费用保持 0/1，不再减星辉）。
        DynamicVars.GetRequired<IntVar>("AmplifyCount").UpgradeValueBy(1);   // 1 -> 3
    }
}
