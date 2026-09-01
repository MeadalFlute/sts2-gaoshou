using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 飞沙：技能（罕见）。耗 0 能量 1 星辉。给予所有敌人 1 层虚弱。
// 流转：所有敌人本回合失去 6(9) 点力量（回合结束后恢复，由 SandThrowStrDropPower 实现）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class SandThrow : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    // 词条：流转（供流转系能力检测）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    // 悬浮释义：虚弱、力量（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Power<WeakPower>(1),
        ModCardVars.Int("StrengthDown", 3),
    ];

    public SandThrow() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 给予所有敌人 1 层虚弱。
        foreach (var enemy in this.CombatState?.HittableEnemies ?? [])
            await PowerCmd.Apply<WeakPower>(choiceContext, enemy,
                DynamicVars.GetRequired<PowerVar<WeakPower>>("WeakPower").BaseValue, Owner.Creature, this);

        // 流转：所有敌人本回合失去 6(9) 点力量（附回补能力，敌人回合结束时恢复）。
        if (GaoshouFlowTracker.IsFlowReady(this))
        {
            var amount = DynamicVars.GetRequired<IntVar>("StrengthDown").BaseValue;
            foreach (var enemy in this.CombatState?.HittableEnemies ?? [])
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, -amount, Owner.Creature, this);
                await PowerCmd.Apply<SandThrowStrDropPower>(choiceContext, enemy, amount, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // DynamicVars.GetRequired<PowerVar<WeakPower>>("WeakPower").UpgradeValueBy(1);  // 虚弱 1 -> 2
        DynamicVars.GetRequired<IntVar>("StrengthDown").UpgradeValueBy(2);   // 3 -> 5
    }
}

// 飞沙·回补：敌人本回合失去的力量，在敌人回合结束时加回（Amount=减少量）。
[RegisterPower]
public sealed class SandThrowStrDropPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/sandthrow.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/sandthrow.png");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0)
            return;
        // 敌人侧回合结束：恢复本回合被降低的力量。
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, null, null);
        await PowerCmd.Remove(this);
    }
}