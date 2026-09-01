using Godot;
using MegaCrit.Sts2.Core.Entities.RestSite;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Relics;

// 拦截 RestSiteOption.get_Icon：碎纸机火堆行动返回自绘图标（纸底+碎纸机）。
// RestSiteOption.Icon 不可覆写（非 virtual），故用 Harmony Prefix 替换返回值。
public class RestIconPatch : IPatchMethod
{
    private static Texture2D? _shredIcon;

    public static string PatchId => "gaoshou_rest_icon";
    public static string Description => "custom icon for the Shredder rest-site option";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(RestSiteOption), "get_Icon"),
    ];

    public static bool Prefix(RestSiteOption __instance, ref Texture2D __result)
    {
        if (__instance is ShredRestSiteOption)
        {
            _shredIcon ??= GD.Load<Texture2D>($"{Entry.ResPath}/images/relics/option_shred.png");
            __result = _shredIcon;
            return false;
        }
        return true;
    }
}