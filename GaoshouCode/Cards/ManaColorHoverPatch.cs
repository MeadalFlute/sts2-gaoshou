using STS2RitsuLib.Scaffolding.Content;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Keywords;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Cards;

// 悬浮释义置顶添加 mana 颜色条（第一条）：
// 形如"【图标】红：这是一张红色卡。"——拦截 ModCardTemplate.get_HoverTips 后置顶插入。
public class ManaColorHoverPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_mana_color_hover";
    public static string Description => "mana color tip as first card hover tip";
    public static bool IsCritical => false;

    private static readonly ConcurrentDictionary<string, Texture2D?> _iconCache = new();

    private static readonly Dictionary<GaoshouCardColor, string> _manaKeys = new()
    {
        [GaoshouCardColor.Red] = "GAOSHOU_MANA_RED",
        [GaoshouCardColor.Blue] = "GAOSHOU_MANA_BLUE",
        [GaoshouCardColor.Purple] = "GAOSHOU_MANA_PURPLE",
        [GaoshouCardColor.Green] = "GAOSHOU_MANA_GREEN",
        [GaoshouCardColor.Colorless] = "GAOSHOU_MANA_COLORLESS",
        [GaoshouCardColor.Black] = "GAOSHOU_MANA_BLACK",
        [GaoshouCardColor.RedBlue] = "GAOSHOU_MANA_REDBLUE",
        [GaoshouCardColor.RedPurple] = "GAOSHOU_MANA_REDPURPLE",
        [GaoshouCardColor.BluePurple] = "GAOSHOU_MANA_BLUEPURPLE",
        [GaoshouCardColor.RedGreen] = "GAOSHOU_MANA_REDGREEN",
        [GaoshouCardColor.BlueGreen] = "GAOSHOU_MANA_BLUEGREEN",
        [GaoshouCardColor.ColorlessPurple] = "GAOSHOU_MANA_COLORLESSPURPLE",
        [GaoshouCardColor.ColorlessBlack] = "GAOSHOU_MANA_COLORLESSBLACK",
    };

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(ModCardTemplate), "get_HoverTips"),
    ];

    public static IEnumerable<IHoverTip> Postfix(IEnumerable<IHoverTip> __result, AbstractModel __instance)
    {
        if (__instance is CardModel card
            && card.Id.Entry.StartsWith("GAOSHOU_CARD")
            && TryGetManaTip(card, out var manaTip))
        {
            return new IHoverTip[] { manaTip }.Concat(__result ?? Enumerable.Empty<IHoverTip>());
        }
        return __result ?? Enumerable.Empty<IHoverTip>();
    }

    private static bool TryGetManaTip(CardModel card, out IHoverTip tip)
    {
        tip = null!;
        // 幻影复制品优先用实例级颜色（双色随机单色）。
        GaoshouCardColor? color = PhantomColorRegistry.TryGet(card, out var assigned)
            ? assigned
            : card.GetType().GetProperty("CardColor")?.GetValue(card) as GaoshouCardColor?;
        if (color is not GaoshouCardColor cc || !_manaKeys.TryGetValue(cc, out var key))
            return false;

        var icon = _iconCache.GetOrAdd(cc.ToString(), k =>
            GD.Load<Texture2D>($"{Entry.ResPath}/images/mana/{k}.png"));
        tip = new HoverTip(
            new LocString("cards", key + ".title"),
            new LocString("cards", key + ".description"),
            icon);
        return true;
    }
}