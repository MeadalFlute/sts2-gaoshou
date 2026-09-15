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
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            var r = GaoshouFlowTracker.IsFlowGlowReady(this);
            Godot.GD.Print($"GAOSHOU-FLOW-GLOW flowReady={r}");
            return r;
        }
    }

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
        var _flowReady = GaoshouFlowTracker.IsFlowReady(this);
        Godot.GD.Print($"GAOSHOU-FLOW-2 prev-flags card={cardPlay.Card.Id?.Entry} flowReady={_flowReady} thisColor={GaoshouFlowTracker.GetColor(cardPlay.Card)} isGreen={cardPlay.Card.Keywords.Contains(GaoshouKeyword.Flow)}");
        if (_flowReady)
        {
            await PowerCmd.Apply<DoubleDamagePower>(choiceContext, Owner.Creature,
                DynamicVars["DoubleDamagePower"].BaseValue, Owner.Creature, this);
            Godot.GD.Print($"GAOSHOU-FLOW-2 applied DoubleDamage");
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"虚无"（费用保持 1/1）。
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
