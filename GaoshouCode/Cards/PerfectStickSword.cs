using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.CardPools;
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

// 完美木棍剑：攻击（无色衍生）。耗 2 能量。
// 造成 5 点伤害重复 4 次，然后对所有敌人造成 8(12) 点伤害。消耗。
[RegisterCard(typeof(TokenCardPool))]
public sealed class PerfectStickSword : ModCardTemplate, Gaoshou.Keywords.IWasteCard
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        ModCardVars.Int("Times", 4),
        new DamageVar("all", 8m, ValueProp.Move),
    ];

    public PerfectStickSword() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var target = cardPlay.Target;

        var times = DynamicVars.GetRequired<IntVar>("Times").BaseValue;

        // 胆小（Skittish，花园幽灵鳗）：「挨第一下后获得格挡」在原版挂在 AfterAttack 上，原版多段攻击是一条 AttackCommand，
        // 所以它在全部命中打完后才结算；我们是每段一条指令 → 用这个作用域把这条反应压到全部段数打完（见 Patches/HitReactionDelay.cs）。
        // （蜷身 CurlUp 挂的是出牌结束，本来就在全部段数之后，不需要处理。）
        await using var hitReactionDelay = HitReactionDelay.Begin(choiceContext);

        // 对目标造成 times 次伤害（焚烧同款：单次 Execute 多段命中）。
        // 活力补偿：原版 VigorPower 的加成绑定在**一次 AttackCommand** 上（出手前快照层数 → 每次伤害实例加 → 攻击后一次性扣光），
        // 所以下面这种"两次独立 DamageCmd.Attack"的写法只有第一段能吃到活力。这里先快照层数，第二段起手工补上同样的加成。
        var vigor = Owner!.Creature.GetPowerAmount<VigorPower>();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount((int)times)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        // 对所有敌人造成伤害（单次 Execute）。
        await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("all").BaseValue + vigor)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(this.CombatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<DamageVar>("all").UpgradeValueBy(4m);   // 8 -> 12
    }
}
