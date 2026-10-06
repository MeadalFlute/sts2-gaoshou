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

// 人月神话：能力（稀有）。耗 1 能量 0 辉星（**升级不再减费**）。
// 效果：本场战斗中临时加入你牌组的牌将被升级。
// 【2026-10-06 用户调整】**默认没有附赠**；升级效果改为"追加附赠 1"（旧版是默认附赠 1 + 升级降到 0 费）。
//   实现：附赠词条在 OnUpgrade 里 AddKeyword（与 SniperRifle 升级加幻影同一写法），张数走动态变量 Bonus（0 -> 1）；
//   机制由词条规则 Keywords\BonusDrawRule.cs 统一驱动，本卡不写任何抽牌逻辑。
//   卡面文案用 {IfUpgraded:show:...} 只在升级后显示那一行。
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

    // 默认**没有**词条：附赠 1 是升级奖励（升级时由 OnUpgrade 追加）。
    // （CanonicalKeywords 不写 Bonus ⇒ 未升级的牌不带附赠词条、也不额外抽牌。）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    // 附赠张数（描述里升级后的 {Bonus}）。基础 0 = 未升级不抽；升级 +1。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Bonus", 0),
    ];

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
        // 升级效果 = 追加"附赠 1"词条（费用不变，仍是 1 能量）。
        // ⚠️ 词条必须在 OnUpgrade 里加（不要依赖 IsUpgraded 惰性缓存，见 SniperRifle 的同款注释）。
        DynamicVars.GetRequired<IntVar>("Bonus").UpgradeValueBy(1);   // 0 -> 1
        AddKeyword(GaoshouKeyword.Bonus);
    }
}
