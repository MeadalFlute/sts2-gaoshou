using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Keywords;

namespace Gaoshou.Keywords;

// 幻影复制品颜色注册表：双色卡的幻影复制品 = 随机抽取其中一个单色（实例级覆盖）。
// 复制品实例在 AddPhantomCopyAsync 时登记；mana 颜色释义读取时优先取这里。
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