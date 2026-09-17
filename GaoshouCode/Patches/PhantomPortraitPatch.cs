using Godot;
using Gaoshou.Keywords;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Patches;

/// <summary>
/// 幻影副本按"分配到的单色"切换卡面。
///
/// 背景：双色卡的幻影/复制品在创建时会被 <see cref="PhantomColorRegistry" /> 分配**一个**主色
/// （<c>PhantomCloneColorPatch</c> / <c>PhantomSingleton</c> 两处登记），mana 释义与[gold]流转[/gold]判定
/// 都已经按它走；只有**卡面**还是本体那张双色图。这里补上卡面：
/// 若该卡实例有分配色，且存在对应的单色图 <c>res://Gaoshou/images/cards/&lt;类名&gt;_&lt;色码&gt;.png</c>
/// （色码 R/B/P/G，对应 <see cref="GaoshouCardColor" /> 的四个单色），就用它替换立绘路径；否则原样返回。
///
/// 挂钩点：<c>ModCardTemplate.CustomPortraitPath</c>（RitsuLib 的虚属性，见 STS2-RitsuLib 源码
/// <c>ModCardTemplate.cs:65</c>）。本模组所有卡牌都没有覆写它 → 打在基类 getter 上即可覆盖全部卡牌；
/// 其它模组的卡与本模组没做单色图的卡都不受影响（找不到图就回落）。
/// </summary>
public sealed class PhantomPortraitPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_phantom_portrait";

    public static string Description => "phantom copies of dual-color cards use their assigned single-color art";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<ModCardTemplate>("get_CustomPortraitPath"),
    ];

    public static void Postfix(ModCardTemplate __instance, ref string? __result)
    {
        if (__instance == null)
            return;

        if (!PhantomColorRegistry.TryGet(__instance, out var color))
            return;

        var variant = VariantPath(__instance, color);
        if (variant != null)
            __result = variant;
    }

    /// <summary>单色 → 文件名后缀；找不到对应图（或不是单色）时返回 null，调用方保持原卡面。</summary>
    private static string? VariantPath(CardModel card, GaoshouCardColor color)
    {
        var suffix = color switch
        {
            GaoshouCardColor.Red => "R",
            GaoshouCardColor.Blue => "B",
            GaoshouCardColor.Purple => "P",
            GaoshouCardColor.Green => "G",
            _ => null,
        };

        if (suffix == null)
            return null;

        var path = $"{Entry.ResPath}/images/cards/{card.GetType().Name}_{suffix}.png";

        // 没做单色图的卡（目前只有杠杆霰弹枪做了 R/P）自动回落本体卡面，不会出现空图。
        return ResourceLoader.Exists(path) ? path : null;
    }
}
