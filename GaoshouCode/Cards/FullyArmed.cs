using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 全副武装：技能（稀有）。耗 0 能量 2 辉星（升级后 1 辉星）。
// 附赠 1（抽到时额外抽 1 张）；你可以保留最多 10 点格挡至下一回合，并将一张本牌复制品加入抽牌堆。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class FullyArmed : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/FullyArmed.png");

    // 悬浮释义：全副武装（至多保留格挡）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouRetainBlockPower>(),
    ];

    // 词条：附赠（抽到这张牌时额外抽 n 张，n = 动态变量 Bonus）。
    // 机制**不在本文件**：由词条规则 Keywords\BonusDrawRule.cs 统一驱动（2026-10-06 整理为关键词）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Bonus,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Blur", 10),
        ModCardVars.Int("Bonus", 1),   // 附赠 1：抽到时额外抽 1 张
    ];

    public override int CanonicalStarCost => 2;

    public FullyArmed() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 10 层"全副武装"：下次回合开始最多保留等量格挡。
        await PowerCmd.Apply<GaoshouRetainBlockPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("Blur").BaseValue, Owner.Creature, this);

        // 复制品加入**抽牌堆**（随机位置；用 PreviewCardPileAdd 让玩家看到它进了哪个牌堆）。
        var copy = CreateClone();
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Draw, Owner, CardPilePosition.Random));
    }

    protected override void OnUpgrade()
    {
        UpgradeStarCostBy(-1);   // 辉星 2 -> 1；保留格挡不变
    }
}
