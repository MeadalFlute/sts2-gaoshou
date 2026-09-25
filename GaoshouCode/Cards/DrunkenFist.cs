using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 醉拳：攻击（罕见）。耗 2 能量 0 辉星。
// 对所有敌人造成 8 点伤害；流转（颜色与上一张牌完全不同）再打一次；奇迹（非回合初抽牌进手）再打一次。
// 三段条件各自独立，所以最多 3 段（8×3），用一次 AttackCommand 的 WithHitCount 表现多段。
// 奇巧（原版 Sly）：被从手牌弃掉时会自动打出，于是"被弃掉"也能吃到基础那一段。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class DrunkenFist : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedPurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 流转或奇迹可触发时泛橙光（同爆发 Burst 的写法）。
    protected override bool ShouldGlowGoldInternal =>
        GaoshouFlowTracker.IsFlowGlowReady(this) || MiracleCounter.IsMiracleGlowReady(this);

    // 悬浮释义：奇迹（原版关键字里没有它，必须自己加）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
        GaoshouKeyword.Miracle,
        CardKeyword.Sly,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
    ];

    public DrunkenFist() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 基础 1 段；流转就绪 +1 段；奇迹就绪 +1 段。
        var hits = 1
            + (GaoshouFlowTracker.IsFlowReady(this) ? 1 : 0)
            + (MiracleCounter.IsMiracleReady(this) ? 1 : 0);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(this.CombatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);   // 8 -> 11（三段全吃，等于 +9）
    }
}
