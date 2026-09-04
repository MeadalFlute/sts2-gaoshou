using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 突破极限：技能（普通）。耗 0 能量。抽 3(4) 张牌；随后将 1 张“凋萎”状态牌加入弃牌堆（暂未实装）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class LimitBreak : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedPurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/LimitBreak.png");

    // 悬浮释义：凋萎状态牌预览（参考故障机器人-超频）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard(ModelDb.Card<Wither>()),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Draw", 3),
    ];

    public LimitBreak() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars.GetRequired<IntVar>("Draw").BaseValue, Owner);

        // 往弃牌堆加入一张「凋萎」状态牌（生成牌动画/预览：参考没脑子拳）。
        var wither = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<Wither>(), Owner);
        if (wither != null)
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(wither, PileType.Discard, Owner));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("Draw").UpgradeValueBy(1);   // 3 -> 4
    }
}
