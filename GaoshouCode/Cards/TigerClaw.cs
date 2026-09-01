using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 黑虎掏心：攻击（普通）。耗 1 能量 0 星辉。造成 6(8) 伤害。\n[流转]（默认直接触发）：造成 5(8) 伤害。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TigerClaw : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/TigerClaw.png");

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    // 词条：流转（供子弹/流转类能力按条件触发）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new DamageVar("secondHit", 5m, ValueProp.Move),
    ];

    public TigerClaw() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 流转（颜色与上一张牌完全不同时触发）：造成 5(8) 点伤害。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("secondHit").BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);                                  // 6 -> 8
        DynamicVars.GetRequired<DamageVar>("secondHit").UpgradeValueBy(3);     // 5 -> 8
    }
}
