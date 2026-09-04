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

        var cmd = await DamageCmd.Attack(total)
            .FromCard(this, cardPlay)
            .Targeting(enemy)
            .Execute(choiceContext);

        // 斩杀判定：以攻击结算的 DamageResult.WasTargetKilled 为准（比 IsDead/IsAlive 可靠，观者-勤学精进同款）。
        var killed = cmd.Results.SelectMany(r => r).Any(r => r.WasTargetKilled && r.Receiver == enemy);
        if (killed && CurrentUpgradeLevel < MaxUpgradeLevel)
        {
            // 手动升级路径：绕过 CardCmd.Upgrade 的 IsEnding 门控（末敌斩杀时会被吞）。
            UpgradeInternal();
            FinalizeUpgradeInternal();
            // 敲牌动画：NCardSmithVfx（勤学精进同款，挂卡片预览容器）。
            try
            {
                if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner))
                {
                    MegaCrit.Sts2.Core.Helpers.GodotTreeExtensions.AddChildSafely(
                        MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi.CardPreviewContainer,
                        MegaCrit.Sts2.Core.Nodes.Vfx.NCardSmithVfx.Create(new[] { this }, true));
                }
            }
            catch
            {
                // 特效失败不影响升级。
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 每第 n 次升级增加 (n+4) 点伤害：12 -> 16 -> 21 -> 27 ...（n(n+7)/2 的差分）。
        DynamicVars.Damage.UpgradeValueBy(CurrentUpgradeLevel + 4);
    }
}