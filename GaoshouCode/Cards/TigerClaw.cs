using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Patches;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 黑虎掏心：攻击（普通）。耗 1 能量 0 辉星。造成 6(8) 伤害。\n[流转]（默认直接触发）：造成 5(8) 伤害。
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
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowGlowReady(this);

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

        // 胆小（Skittish，花园幽灵鳗）：「挨第一下后获得格挡」在原版挂在 AfterAttack 上，原版多段攻击是一条 AttackCommand，
        // 所以它在全部命中打完后才结算；我们是每段一条指令 → 用这个作用域把这条反应压到全部段数打完（见 Patches/HitReactionDelay.cs）。
        // （蜷身 CurlUp 挂的是出牌结束，本来就在全部段数之后，不需要处理。）
        await using var hitReactionDelay = HitReactionDelay.Begin(choiceContext);

        // 活力补偿：原版 VigorPower 的加成绑定在**一次 AttackCommand** 上（出手前快照层数 → 每次伤害实例加 → 攻击后一次性扣光），
        // 所以下面这种"两次独立 DamageCmd.Attack"的写法只有第一段能吃到活力。这里先快照层数，第二段起手工补上同样的加成。
        var vigor = Owner!.Creature.GetPowerAmount<VigorPower>();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 流转（颜色与上一张牌完全不同时触发）：造成 5(8) 点伤害。
        if (GaoshouFlowTracker.IsFlowReady(this))
            await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("secondHit").BaseValue + vigor)
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
