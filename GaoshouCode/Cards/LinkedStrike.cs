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

// 连环打击：攻击。耗 2 能量 0 辉星。造成 4 伤害一次，然后造成 5 伤害一次。升级后：6、9。
[RegisterCard(typeof(GaoshouCardPool))]
[RegisterCharacterStarterCard(typeof(GaoshouCharacter), 4, Order = 10)]
public sealed class LinkedStrike : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Basic;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/LinkedStrike.png");

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    // 第一段伤害使用默认 "Damage" 变量，第二段使用具名 "secondHit"。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 两段不同伤害（4+5，升级 6+7）：引擎无"多义伤害单次执行"，此为游戏原生两段式。
        new DamageVar(4m, ValueProp.Move),
        new DamageVar("secondHit", 5m, ValueProp.Move),
    ];

    public LinkedStrike() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认“无辉星费用”。
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

        await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("secondHit").BaseValue + vigor)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);                                  // 4 -> 6
        DynamicVars.GetRequired<DamageVar>("secondHit").UpgradeValueBy(2);     // 5 -> 7
    }
}
