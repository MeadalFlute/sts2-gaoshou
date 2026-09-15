using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 给本模组的事件挂上插图叠加层（只对 <c>GAOSHOU_EVENT_*</c> 生效，原版事件一行都不碰）。
///
/// 10 个事件共用一张 notebook 底图（通过 <c>EventAssetProfile.InitialPortraitPath</c> 指定），
/// 每个事件自己的插图在这里按 id 约定（<c>GAOSHOU_EVENT_X</c> → <c>images/events/art/x.png</c>）
/// 贴到 <c>%Portrait</c> 左半。要换场景（神秘洞穴）由事件代码自己调 <c>EventArtOverlay.ShowInRoom</c> 覆盖。
///
/// 挂钩点：<c>NEventLayout.SetEvent(EventModel)</c> 的 Postfix —— 此时场景已 <c>_Ready</c>、
/// <c>InitialPortraitPath</c> 也已经设成底图，正适合叠加。
/// </summary>
public sealed class EventArtOverlayPatch : IPatchMethod
{
    private const string ModEventIdPrefix = "GAOSHOU_EVENT_";

    public static string PatchId => "gaoshou_event_art_overlay";

    public static string Description => "overlay the per-event illustration on the shared notebook event background";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NEventLayout>(nameof(NEventLayout.SetEvent), typeof(EventModel)),
    ];

    public static void Postfix(NEventLayout __instance, EventModel eventModel)
    {
        // Id 在部分实例（图鉴/克隆）上可能为空，一律空条件访问。
        if (eventModel?.Id?.Entry is not { } entry
            || !entry.StartsWith(ModEventIdPrefix, StringComparison.Ordinal))
            return;

        Events.EventArtOverlay.Show(__instance, Events.EventArtOverlay.ArtNameForEntry(entry));
    }
}
