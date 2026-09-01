using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 仪式匕首：攻击（罕见）。耗 2 能量。造成 12(+n(n+7)/2) 点伤害（n=升级次数）。
// 斩杀时：升级自己，可无限升级。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class RitualDagger : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private const decimal BaseDamage = 12m;

    public GaoshouCardColor CardColor => GaoshouCardColor.Black;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    // 可无限升级。
    public override int MaxUpgradeLevel => 1000;

    // 悬浮释义：斩杀（Fatal，参考原版狂宴）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Fatal),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(BaseDamage, ValueProp.Move),
    ];

    public RitualDagger() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var enemy = cardPlay.Target;

        // 伤害 = 12 + n(n+7)/2（n = 升级次数，可无限叠加；DamageVar 每级 +n+4）。
        var n = CurrentUpgradeLevel;
        var total = DynamicVars.Damage.BaseValue + n * (n + 7) / 2m;

        await DamageCmd.Attack(total)
            .FromCard(this, cardPlay)
            .Targeting(enemy)
            .Execute(choiceContext);

        // 斩杀时：升级自己（可无限升级）。
        if (enemy.IsDead && CurrentUpgradeLevel < MaxUpgradeLevel)
            CardCmd.Upgrade(this);
    }

    protected override void OnUpgrade()
    {
        // 每第 n 次升级增加 (n+4) 点伤害：12 -> 16 -> 21 -> 27 ...（n(n+7)/2 的差分）。
        DynamicVars.Damage.UpgradeValueBy(CurrentUpgradeLevel + 4);
    }
}