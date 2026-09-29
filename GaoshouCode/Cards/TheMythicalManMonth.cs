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

// 人月神话：能力（稀有）。耗 1(0) 能量 0 辉星。
// 效果：附赠 1（**不是**原版 Innate/固有 ✗，2026-09-29 用户实测纠正）+ 本场战斗中临时加入你牌组的牌将被升级。
// 「附赠」的确切语义（按本 mod 既有卡「急！PureAnger」= 用户指定参考）：
//   词条 GaoshouKeyword.Bonus（GaoshouKeyword.cs:26/56-57，仅声明+悬浮释义）
//   + 卡自己 override AfterCardDrawn，且 `card == this` 时才额外抽 {Cards} 张
//   （PureAnger.cs:37-64；Cryptic.cs:40/48/143 同一套）。
// 能力实现见 Powers/MythicalManMonthPower.cs（参考 资料\Watcher\WatcherMod\MasterRealityPower.cs:16-24）。
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

    // 词条：附赠（附赠 1 = 抽到这张牌时额外抽 1 张；机制由下面的 AfterCardDrawn 实现）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Bonus,
    ];

    // 附赠张数（描述里的 {Cards}）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Cards", 1),
    ];

    // 附赠：抽到**本牌自身**时额外抽 {Cards} 张（必须 card == this 守卫，否则任意抽牌都会级联 —
    // 照 PureAnger.cs:58-64 的原话）。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this)
            return Task.CompletedTask;

        return CardPileCmd.Draw(choiceContext, DynamicVars.GetRequired<IntVar>("Cards").BaseValue, Owner);
    }

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
        EnergyCost.UpgradeBy(-1);   // 费用 1 -> 0（升级只降费，效果不变）
    }
}
