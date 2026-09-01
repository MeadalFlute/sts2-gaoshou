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

// 标枪：攻击（普通）。耗 1 能量。造成 6(10) 点伤害；流转：抽 2 张牌。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Javalin : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    // 词条：流转（供流转系能力检测——此前缺失导致流转从未触发）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        ModCardVars.Int("Draw", 2),
    ];

    public Javalin() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 流转（颜色与上一张牌完全不同时触发）：抽 1(2) 张牌。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await CardPileCmd.Draw(choiceContext, (int)DynamicVars.GetRequired<IntVar>("Draw").BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);                     // 6 -> 10
        // DynamicVars.GetRequired<IntVar>("Draw").UpgradeValueBy(1); // 1 -> 2
    }
}