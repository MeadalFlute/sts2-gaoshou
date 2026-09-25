using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 心流：技能（普通）。耗 1 能量 1 辉星（升级 0/1）。本回合你造成的伤害翻倍。消耗。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Flow : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Flow.png");

    // 高亮（流转就绪）——打点评估时机。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowGlowReady(this);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
        CardKeyword.Exhaust,
        CardKeyword.Ethereal,
    ];

    public Flow() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 1;

    // 双倍伤害 1 层。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Power<DoubleDamagePower>(1m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 流转（颜色与上一张牌完全不同）：本回合造成的伤害翻倍（暗影步同款 DoubleDamagePower）。
        if (GaoshouFlowTracker.IsFlowReady(this))
        {
            // 注意：原版 DoubleDamagePower 的层数 = **剩余回合数**（它的 AfterSideTurnEnd 每回合末只 -1），
            // 所以每次触发都 +1 会让时长无限累加（实测能堆到 88 层、永不结束）。
            // 本卡是"本回合造成伤害翻倍" → 只在没有该 buff 时应用 1 层，回合末自然消失。
            var already = Owner.Creature.GetPowerAmount<DoubleDamagePower>();
            if (already <= 0)
            {
                await PowerCmd.Apply<DoubleDamagePower>(choiceContext, Owner.Creature,
                    DynamicVars["DoubleDamagePower"].BaseValue, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"虚无"（费用保持 1/1）。
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
