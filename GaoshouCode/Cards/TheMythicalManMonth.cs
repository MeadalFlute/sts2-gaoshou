using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 人月神话：能力（稀有）。耗 1 能量 0 辉星（费用不随升级变化）。
// 效果：本场战斗中临时加入你牌组的牌将被升级。
// 【2026-10-07 用户调整】升级效果由"追加附赠 1"改为**获得固有（Innate）**：
//   即升级后这张能力会在战斗开始时必定上手（原版 Innate 的语义由游戏自身处理，我们只需 AddKeyword）✓
//   词条在 OnUpgrade 里 AddKeyword（与 SniperRifle 升级加幻影同一写法，避免 IsUpgraded 惰性缓存问题）✓
//   卡面**不用**再写 {IfUpgraded:show:...} 行：固有是原版词条，卡面会自动显示"固有"词条提示 ✓
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TheMythicalManMonth : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    // 卡牌颜色 B（蓝 / 辉星）。
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 默认**没有**词条：固有是升级奖励（升级时由 OnUpgrade 追加）。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    // 悬浮释义：预览它施加的能力。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<MythicalManMonthPower>(),
    ];

    public TheMythicalManMonth() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力（层数 1）：本场战斗中临时加入牌组的牌会被升级。
        await PowerCmd.Apply<MythicalManMonthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级效果 = 获得固有（战斗开始时必定在手）。费用仍是 1 能量。
        AddKeyword(CardKeyword.Innate);
    }
}
