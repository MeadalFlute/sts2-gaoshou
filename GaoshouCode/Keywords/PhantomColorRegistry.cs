using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Keywords;

namespace Gaoshou.Keywords;

// 幻影复制品颜色注册表：双色卡的幻影复制品 = 随机抽取其中一个单色（实例级覆盖）。
// 登记点在 PhantomSingleton（Keywords\Phantom.cs）：它创建复制品后用**同步 RNG**取色
// （RunState.Rng.CombatCardGeneration，保证主机/客机同色）；mana 颜色释义与「流转」判定读取时优先取这里。
// 取不到实例色时，调用方回退到卡牌类型自带的 CardColor（确定性），所以本表缺失不会造成两端分歧。
public static class PhantomColorRegistry
{
    private static readonly Dictionary<CardModel, GaoshouCardColor> _colors = new();

    public static void Assign(CardModel copy, GaoshouCardColor? color)
    {
        if (color.HasValue)
            _colors[copy] = color.Value;
        else
            _colors.Remove(copy);
    }

    public static bool TryGet(CardModel copy, out GaoshouCardColor color)
    {
        return _colors.TryGetValue(copy, out color);
    }

    /// <summary>
    /// 清空全部实例色记录，由 <c>PhantomSingleton.BeforeCombatStart</c> 在每场战斗开始时调用。
    ///
    /// 为什么需要清：本表是 **static** 字段（存活于整个游戏进程，跨战斗、跨局），
    /// key 是 CardModel 实例 → 不清就会一直攒着旧卡对象的强引用（内存泄漏）。
    /// 实例色只在战斗内有意义（幻影复制品只活在战斗牌堆里）所以开新战斗时清掉是安全的。
    /// 注意：字典走**引用相等**（CardModel 没重写 Equals/GetHashCode），所以旧条目不会串到新卡上，
    /// 这个清理只是为了不白占内存，不是修正确性问题。
    /// </summary>
    public static void Clear()
    {
        _colors.Clear();
    }

    // 双色 -> 组成主色列表（供随机抽取）。
    public static List<GaoshouCardColor> GetPrimaries(GaoshouCardColor color)
    {
        return color switch
        {
            GaoshouCardColor.RedBlue or GaoshouCardColor.RedPurple or GaoshouCardColor.RedGreen => new() { GaoshouCardColor.Red, OtherPrimary(color) },
            GaoshouCardColor.BluePurple or GaoshouCardColor.BlueGreen => new() { GaoshouCardColor.Blue, OtherPrimary(color) },
            _ => new() { color },
        };
    }

    private static GaoshouCardColor OtherPrimary(GaoshouCardColor color)
    {
        return color switch
        {
            GaoshouCardColor.RedBlue => GaoshouCardColor.Blue,
            GaoshouCardColor.RedPurple => GaoshouCardColor.Purple,
            GaoshouCardColor.RedGreen => GaoshouCardColor.Green,
            GaoshouCardColor.BluePurple => GaoshouCardColor.Purple,
            GaoshouCardColor.BlueGreen => GaoshouCardColor.Green,
            _ => GaoshouCardColor.Red,
        };
    }
}