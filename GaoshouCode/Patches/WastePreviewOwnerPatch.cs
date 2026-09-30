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

    /// <summary>
    /// ⚠️ 这里**只做纯字段赋值**（编译器会内联成 `stsfld`：不含任何方法调用 ⇒ 不会触发新的 JIT ✗）。
    /// 原因（2026-09-30 完整栈实证）：本回调是 `NHoverTipSet.CreateAndShow` 的 detour，
    /// **在这个上下文里首次 JIT 我方任何方法都会炸** —— MonoMod 的 JIT hook 抛
    /// `ArgumentException: Value does not fall within the expected range`
    /// （栈里"正在编译的方法"就是我们的 `WastePreviewPatch.IsPreviewNodeItself` ✗）。
    /// ⇒ owner 识别 / 登记 / 诊断全部交给 `WastePreviewPatch.ProcessPendingScan()`，
    ///   由 **`_Process` 补丁**（每帧跑 `Collect`/`ApplyCurrent`、从未报错的安全上下文 ✓）在下一帧执行 ✓
    /// </summary>
    public static void Postfix(Control owner, ref NHoverTipSet? __result)
    {
        if (__result == null)
            return;

        WastePreviewPatch.PendingSet = __result;
        WastePreviewPatch.PendingOwner = owner;
        WastePreviewPatch.PendingScan = true;
    }
}
