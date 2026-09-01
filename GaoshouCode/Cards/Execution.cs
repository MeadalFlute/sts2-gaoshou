using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 处决：技能（稀有）。耗 0 能量 10 星辉（升级 8 星辉）。若目标处于击晕状态：直接击杀目标。消耗（升级后追加保留）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Execution : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 存在被击晕的敌人时泛橙光（满足条件才亮）。
    protected override bool ShouldGlowGoldInternal =>
        (this.CombatState?.HittableEnemies ?? []).Any(e => e.IsStunned);

    // 悬浮释义：击晕。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(MegaCrit.Sts2.Core.HoverTips.StaticHoverTip.Stun),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public Execution() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 10;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 若目标处于击晕状态：直接击杀目标（非伤害，可无视无实体等）。
        if (cardPlay.Target.IsStunned)
            await CreatureCmd.Kill(cardPlay.Target);
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-2);   // 星辉 10 -> 8（不再追加保留）
    }
}