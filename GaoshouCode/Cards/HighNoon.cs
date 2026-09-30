using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 正午：攻击（普通）。耗 0 能量 0 辉星。**只有手牌中所有牌仅有一种颜色时才能打出**：
// 造成 14(18) 点伤害。
//
// 颜色判定口径（按用户原文，判定实现在 Keywords/HighNoonRule.cs）：
//   * 没有颜色属性、或颜色为「无色(N)」的牌 **不算颜色**；
//   * 双色牌（RB/RP/BP/RG/BG/NP/NB）的**每种颜色各算一种**；
//   * 手牌里出现的"不同颜色种类数" <= 1 才可打出（正午自己不算：判定时它还在手牌里，要排除）。
// 「颜色」在本仓库怎么表示：卡上有一个 public GaoshouCardColor CardColor 属性
// （枚举见 Keywords/GaoshouCardColor.cs），统一通过 GaoshouFlowTracker.GetColor(card) 读取
// （反射 + 类型缓存，幻影复制品优先用实例色，见 Keywords/GaoshouFlowTracker.cs:93-109）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class HighNoon : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    // 卡牌颜色 P（紫）。【2026-09-29 用户调整】原为 N（无色），改为紫。
    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义（【2026-09-29 用户要求新增】）：说明"无色牌 / 没有颜色属性的牌不算一种颜色"。
    // 文本放 localization/{eng,zhs}/cards.json 的 GAOSHOU_CARD_HIGH_NOON_COLOR_HINT
    //（写法照本仓库既有的纯文本悬浮释义，如 Cards/WastePreview.cs:78）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        new HoverTip(new LocString("cards", "GAOSHOU_CARD_HIGH_NOON_COLOR_HINT")),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14m, ValueProp.Move),
    ];

    public HighNoon() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost。
    }

    // 泛光：满足可打出条件（手牌颜色种数 <= 1）时亮起；弃牌/选牌界面（手牌选择模式）下不亮。
    protected override bool ShouldGlowGoldInternal =>
        GaoshouKeywordMechanics.IsHandGlowAllowed() && HighNoonRule.CountHandColors(this) <= 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 引擎层面已经用 ShouldPlay（HighNoonRule）挡掉了不满足条件的情况；
        // 这里再判一次只是兜底（例如被别的效果强制打出时）。
        if (HighNoonRule.CountHandColors(this) > 1)
            return;

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);   // 14 -> 18
    }
}
