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