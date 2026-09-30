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
        // 悬浮窗创建路径：异常会打断原版 tooltip 的创建/布局 ⇒ 一律吞掉（只记一次日志）。
        try
        {
            // 【一次性取证】正式版 hovertip "错位到屏幕左上角" 专用：只记录**前 3 次**悬浮窗创建的几何信息。
            // 判读方法：正常时 set 的 parent 是原版悬浮层、size>0、位置贴着 owner；
            // 错位时通常能看到 parent=null/size=(0,0)/anchors 全 0/位置=(0,0) ⇒ 直接指出是哪一层坏了。
            if (__result != null && _diagCount < 3)
            {
                _diagCount++;
                var set = __result;
                Entry.Logger.Info(
                    $"[TipDiag] #{_diagCount} owner={owner.Name} ownerGlobal={owner.GlobalPosition} ownerSize={owner.Size} " +
                    $"set={set.Name} parent={set.GetParent()?.Name ?? "<null>"} size={set.Size} pos={set.Position} " +
                    $"anchors=({set.AnchorLeft},{set.AnchorTop},{set.AnchorRight},{set.AnchorBottom}) " +
                    $"offset=({set.OffsetLeft},{set.OffsetTop},{set.OffsetRight},{set.OffsetBottom}) children={set.GetChildCount()}");
            }

            if (__result != null)
            {
                // 一次性取证：登记成功 / 未命中各留一行 ⇒ 能区分"登记根本没发生"与"登记了但查找失败"。
                if (WastePreviewPatch.IsPreviewOwner(owner))
                    WastePreviewPatch.RecordSet(__result, owner);
                else
                    WastePreviewPatch.DiagOwnerMiss(owner);
            }
        }
        catch (System.Exception e)
        {
            if (!_warned)
            {
                _warned = true;
                Entry.Logger.Warn($"[WastePreview] owner recording skipped after an error: {e.Message}");
            }
        }
    }

    private static bool _warned;

    private static int _diagCount;
}
