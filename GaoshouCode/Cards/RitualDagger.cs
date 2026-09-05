using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs.History;
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

        // 伤害 = 12 + n(n+7)/2（n = 升级次数；DamageVar 经 OnUpgrade 已随升级累加为 12 + n(n+7)/2，故直接用 BaseValue）。
        var total = DynamicVars.Damage.BaseValue;

        var cmd = await DamageCmd.Attack(total)
            .FromCard(this, cardPlay)
            .Targeting(enemy)
            .Execute(choiceContext);

        // 斩杀判定：以攻击结算的 DamageResult.WasTargetKilled 为准（比 IsDead/IsAlive 可靠，观者-勤学精进同款）。
        var killed = cmd.Results.SelectMany(r => r).Any(r => r.WasTargetKilled && r.Receiver == enemy);
        if (killed)
        {
            // 精确升级"本卡对应的牌组母本"（this.DeckVersion，由 PopulateCombatState 指向牌库母本）；
            // 临时卡（借用复制/局内生成，DeckVersion null）无母本 → 只升 this，跳过升牌库。
            UpgradeDeckCopies();
            if (IsUpgradable)
            {
                UpgradeInternal();
                FinalizeUpgradeInternal();
            }
            // 敲牌动画：NCardSmithVfx（Lesson Learned 同款；用本卡对应的母本，而非 this）。
            try
            {
                if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner))
                {
                    var upgraded = DeckVersion;
                    MegaCrit.Sts2.Core.Helpers.GodotTreeExtensions.AddChildSafely(
                        MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi.CardPreviewContainer,
                        MegaCrit.Sts2.Core.Nodes.Vfx.NCardSmithVfx.Create(new[] { upgraded ?? (CardModel)this }, true));
                }
            }
            catch
            {
                // 特效失败不影响升级。
            }
        }
    }

    // 精确升级"本卡对应的牌组母本"（战斗副本 DeckVersion 指向牌库母本；跨战斗持久）。
    // 临时卡（无 DeckVersion）则跳过升级牌库，只由 OnPlay 升级 this。
    private void UpgradeDeckCopies()
    {
        if (DeckVersion is not { } master || master is not RitualDagger || !master.IsUpgradable)
            return;
        var history = Owner.RunState.CurrentMapPointHistoryEntry;
        history?.GetEntry(Owner.NetId).UpgradedCards.Add(master.Id);
        master.UpgradeInternal();
        master.FinalizeUpgradeInternal();
    }

    protected override void OnUpgrade()
    {
        // 每第 n 次升级增加 (n+3) 点伤害（f(n)=12+n(n+7)/2 的差分 = n+3）：12 -> 16 -> 21 -> 27 ...
        DynamicVars.Damage.UpgradeValueBy(CurrentUpgradeLevel + 3);
    }
}