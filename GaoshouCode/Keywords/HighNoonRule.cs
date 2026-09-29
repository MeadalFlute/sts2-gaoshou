using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

/// <summary>
/// 「正午」的可打出条件：**手牌中出现的不同颜色种类数 &lt;= 1**（否则引擎层面就打不出，不是"打出后无事发生"）。
///
/// 口径（用户原文）：
///   * 没有颜色属性 / 颜色为无色(N) 的牌 **不算颜色**（<see cref="BaseColorsOf" /> 对 Colorless 返回空集）；
///   * 双色牌（RB/RP/BP/RG/BG/NP/NB）的**每种颜色各算一种**（照抄 GaoshouFlowTracker.cs:29-44 的 _baseColors 表）；
///   * 正午自己（判定时还在手牌）排除，否则它自己的无色无所谓、但逻辑上应当只统计"别的牌"。
///
/// 实现方式与本仓库既有的「限制」词条规则（Keywords/LimitedPlayRule.cs）一致：
/// [RegisterSingleton] + 订阅战斗钩子 + override ShouldPlay。ShouldPlay 是引擎在"能不能打出"时问的钩子，
/// 返回 false 即打不出（多人下同样生效：钩子走的是同步执行路径）。
/// </summary>
[RegisterSingleton]
public sealed class HighNoonRule : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public HighNoonRule()
    {
        ModHelper.SubscribeForCombatStateHooks(Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        // 只对「正午」生效：其它牌一律放行（第一句就返回，零开销）。
        if (card is not HighNoon)
            return true;

        return CountHandColors(card) <= 1;
    }

    /// <summary>
    /// 统计<paramref name="self" />所属玩家**手牌**中出现的"不同基础颜色"种类数。
    /// 无色/无颜色属性的牌不计入；双色牌拆成两种分别计入；排除 <paramref name="self" /> 自己。
    /// </summary>
    public static int CountHandColors(CardModel self)
    {
        if (self.Owner is not { } owner)
            return 0;

        var hand = PileType.Hand.GetPile(owner)?.Cards;
        if (hand == null)
            return 0;

        var colors = new HashSet<GaoshouCardColor>();
        foreach (var card in hand)
        {
            if (ReferenceEquals(card, self))
                continue;

            // 统一走 GaoshouFlowTracker.GetColor：它处理"幻影复制品用实例色"这件事。
            foreach (var baseColor in BaseColorsOf(GaoshouFlowTracker.GetColor(card)))
                colors.Add(baseColor);
        }

        return colors.Count;
    }

    /// <summary>
    /// 颜色 -> 它包含的**基础色**集合（照抄 Keywords/GaoshouFlowTracker.cs:29-44 的 _baseColors 表）。
    /// ⚠️ 按用户口径：**无色(N)不算颜色** => 返回空集；ColorlessPurple/ColorlessBlack 只算其中的紫/黑。
    /// </summary>
    public static IEnumerable<GaoshouCardColor> BaseColorsOf(GaoshouCardColor color) => color switch
    {
        GaoshouCardColor.Red => [GaoshouCardColor.Red],
        GaoshouCardColor.Blue => [GaoshouCardColor.Blue],
        GaoshouCardColor.Purple => [GaoshouCardColor.Purple],
        GaoshouCardColor.Green => [GaoshouCardColor.Green],
        GaoshouCardColor.Black => [GaoshouCardColor.Black],
        GaoshouCardColor.RedBlue => [GaoshouCardColor.Red, GaoshouCardColor.Blue],
        GaoshouCardColor.RedPurple => [GaoshouCardColor.Red, GaoshouCardColor.Purple],
        GaoshouCardColor.BluePurple => [GaoshouCardColor.Blue, GaoshouCardColor.Purple],
        GaoshouCardColor.RedGreen => [GaoshouCardColor.Red, GaoshouCardColor.Green],
        GaoshouCardColor.BlueGreen => [GaoshouCardColor.Blue, GaoshouCardColor.Green],
        GaoshouCardColor.ColorlessPurple => [GaoshouCardColor.Purple],
        GaoshouCardColor.ColorlessBlack => [GaoshouCardColor.Black],
        _ => [],   // Colorless（无色）：不算颜色
    };
}
