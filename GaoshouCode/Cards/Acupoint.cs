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

namespace Gaoshou.Cards;

// 点穴手：技能（普通）。耗 1 能量。施加 1 层虚弱、1 层易伤。增幅2（升级后4）。
// 增幅机制（弃置手牌后重复打出）暂未实装，仅展示词条与数值。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Acupoint : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Acupoint.png");

    // 悬浮释义：增幅（词条）、虚弱、易伤（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Amplify),
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>(),
    ];

    // 虚弱/易伤用游戏原生 PowerVar（描述里 {WeakPower:diff()} 等可标绿升级数值）；
    // 增幅不挂关键词（避免词条 chip 与描述重复），只用文字 + 数值展示 + 悬浮释义。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Power<WeakPower>(1),
        ModCardVars.Power<VulnerablePower>(1),
        ModCardVars.Int("AmplifyCount", 2),
    ];

    public Acupoint() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 主效果：施加 1 层虚弱、1 层易伤。
    private async Task MainOnceAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemy = cardPlay.Target!;
        await PowerCmd.Apply<WeakPower>(choiceContext, enemy,
            DynamicVars.GetRequired<PowerVar<WeakPower>>("WeakPower").BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy,
            DynamicVars.GetRequired<PowerVar<VulnerablePower>>("VulnerablePower").BaseValue, Owner.Creature, this);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await MainOnceAsync(choiceContext, cardPlay);

        // 增幅 2(4)：弃置手牌中最多 AmplifyCount 张，然后按实际弃置数重放。
        var amplifyCount = (int)DynamicVars.GetRequired<IntVar>("AmplifyCount").BaseValue;
        await this.AmplifyAsync(choiceContext, amplifyCount, ctx => MainOnceAsync(ctx, cardPlay));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("AmplifyCount").UpgradeValueBy(2);   // 2 -> 4
    }
}