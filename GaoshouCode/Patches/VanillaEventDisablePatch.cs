using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 让玩家能在设置里逐个禁用指定的原版事件。
///
/// 为什么不用 <c>EventModel.IsAllowed</c>：那 13 个目标里有 8 个**自己重写了** IsAllowed 且不调 base，
/// 给基类方法打补丁拦不到它们。这里改成在事件抽取前直接清理本章事件池：
/// <see cref="RoomSet.EnsureNextEventIsValid" /> 是 <see cref="ActModel.PullNextEvent" /> 里**先于**
/// <c>AddVisitedEvent</c> 调用的，所以在这里剔除不会污染"本局已访问"记录。
///
/// 多人一致性：设置是**主机权威**的，由 <see cref="GaoshouEventSettingsSync" /> 通过 RitsuLib 的 Sidecar 配置主题
/// （<c>gaoshou.event_settings</c>）广播：主机按自己的设置筛，客机照主机快照筛，两端判定一致。
/// 拿不到主机设置时（客机还没收到快照 / Sidecar 不可达 / 多人但网络服务未就绪）**本次不动事件池** ——
/// 等价于加同步之前的「多人局不筛」，宁可不筛，也不让两端事件池与 <c>VisitedEventIds</c> 分歧。
/// </summary>
public sealed class VanillaEventDisablePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_disable_selected_vanilla_events";

    public static string Description => "let players disable selected vanilla events via mod settings (host-authoritative)";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<RoomSet>(nameof(RoomSet.EnsureNextEventIsValid), typeof(RunState)),
    ];

    public static void Prefix(RoomSet __instance, RunState runState)
    {
        if (runState == null)
            return;

        // 与主机对齐「本次要不要筛」（房主会顺带把自己的设置广播出去）；拿不到主机设置就不动事件池。
        if (!GaoshouEventSettings.TryAlignVanillaEventPoolFilter(runState))
            return;

        var disabled = GaoshouEventSettings.DisabledVanillaEventEntries();
        if (disabled.Count == 0)
            return;

        var pool = __instance.events;
        if (pool.Count == 0)
            return;

        var doomed = new List<EventModel>();
        foreach (var entry in disabled)
        {
            foreach (var model in pool.Where(e => e.Id.Entry == entry))
            {
                if (!doomed.Contains(model))
                    doomed.Add(model);
            }
        }

        // 绝不能清空：RoomSet.NextEvent 会做 events[eventsVisited % events.Count]，Count==0 会除零。
        if (doomed.Count >= pool.Count)
            return;

        foreach (var model in doomed)
            pool.Remove(model);
    }
}
