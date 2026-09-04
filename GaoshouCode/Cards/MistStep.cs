using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 迷踪步：技能（普通）。耗 0 能量 1 星辉。获得 6(9) 格挡。\n[流转]（默认直接触发）：抽牌直到你有 3 张牌。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class MistStep : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 流转就绪（颜色与上一张牌完全不同）时泛橙光。
    protected override bool ShouldGlowGoldInternal => GaoshouFlowTracker.IsFlowReady(this);

    // 词条：流转（供子弹/流转类能力按条件触发）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Flow,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        ModCardVars.Int("Cards", 3),
    ];

    public MistStep() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 流转（颜色与上一张牌完全不同时触发）：抽牌直到手牌有 3(4) 张。
        if (GaoshouFlowTracker.IsFlowReady(this))
        {
            var target = DynamicVars.GetRequired<IntVar>("Cards").BaseValue;
            var hand = Owner.PlayerCombatState!.Hand;
            while (hand.Cards.Count < target)
            {
                var before = hand.Cards.Count;
                await CardPileCmd.Draw(choiceContext, 1, Owner);
                if (hand.Cards.Count == before)
                    break;   // 抽牌堆与弃牌堆都为空时避免死循环。
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);   // 6 -> 9
        DynamicVars.GetRequired<IntVar>("Cards").UpgradeValueBy(1);   // 流转目标 3 -> 4
    }
}
