using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 给悬浮窗**记归属**：<c>NHoverTipSet.CreateAndShow</c> 创建完成后，如果这个悬浮窗是挂在那三张
/// （临时武器 / 垃圾宝箱 / 可别浪费）卡面上的，就登记到 <see cref="WastePreviewPatch.OurSets" />。
///
/// 为什么必须要这一步：轮播/滚轮不能靠"悬浮窗里有没有废品牌卡片节点"来判断是不是自己 ——
/// 损失规避的悬浮里就有一张**硬纸板**预览，而硬纸板也是废品牌；只看内容会把它也一起轮换了
/// （曾经的 bug：损失规避的悬浮错误联想为所有废品牌）。按归属判断才是准确的。
///
/// 两个重载都挂上，避免某些界面走的是"单条 tip"的入口而漏记。
/// </summary>
public sealed class WastePreviewOwnerPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_waste_preview_owner";

    public static string Description => "remember which hover-tip sets belong to our waste-preview cards";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method(
            typeof(NHoverTipSet),
            nameof(NHoverTipSet.CreateAndShow),
            typeof(Control),
            typeof(IEnumerable<IHoverTip>),
            typeof(HoverTipAlignment)),
        PatchTarget.Method(
            typeof(NHoverTipSet),
            nameof(NHoverTipSet.CreateAndShow),
            typeof(Control),
            typeof(IHoverTip),
            typeof(HoverTipAlignment)),
    ];

    public static void Postfix(Control owner, ref NHoverTipSet? __result)
    {
        if (__result != null && WastePreviewPatch.IsPreviewOwner(owner))
            WastePreviewPatch.RecordSet(__result);
    }
}
