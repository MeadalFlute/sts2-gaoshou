using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Gaoshou.Events;

/// <summary>
/// 事件收尾助手（2026-10-06 玩家反馈后新增）。
///
/// <para>
/// **问题**：多人游戏里"1 个人选了事件选项，事件就被跳过"。
/// </para>
/// <para>
/// **根因**：本模组的**非共享**事件在选项处理函数里直接调了 <c>NEventRoom.Proceed()</c>。
/// 原版 <c>EventSynchronizer.ChooseOptionForEvent(player, index)</c> 会把"选了该选项那名玩家"的处理函数
/// 在**所有端**都执行一遍（非共享事件同样如此，因为每个玩家各有一份事件实例、
/// 但选了什么必须让所有端同步），而 <c>NEventRoom.Proceed()</c> 是**纯本地 UI 动作**
/// （<c>SetTravelEnabled(true)</c> + <c>NMapScreen.Open()</c>，且没有任何 LocalContext 守卫）
/// ⇒ 别人一选，**你这边的客户端就被直接拽到地图**，事件看起来被跳过了 ✗
/// </para>
/// <para>
/// **改法**（用户 2026-10-06 确认）：单人保持"自动离开"（少点一次，手感同旧版）；
/// 多人**什么都不做**，让房间自己给出「继续」按钮 —— <c>NEventRoom.SetOptions</c> 在事件
/// <c>IsFinished</c> 时会自动补一个 PROCEED 选项，它走 <c>option.IsProceed</c> 分支，
/// **只在本地点、不经同步器**，这正是原版所有事件的收尾方式 ✓
/// </para>
/// <para>
/// 用法：处理函数里 <c>SetEventFinished(PageDescription("..."))</c> 之后
/// <c>await EventFlow.ProceedIfSinglePlayer(Owner);</c> —— 千万别再无条件调用 <c>NEventRoom.Proceed()</c>。
/// </para>
/// </summary>
internal static class EventFlow
{
    /// <summary>
    /// 单人：稍等一下然后自动离开事件。多人（或不是本地玩家的事件）：直接返回，把收尾交给房间的「继续」按钮。
    /// </summary>
    public static async Task ProceedIfSinglePlayer(Player? owner)
    {
        try
        {
            if (owner?.RunState == null)
                return;

            // 多人：交给房间里那个「继续」按钮（本地点、不经同步器）—— 绝不能碰本地 UI ✗
            if (owner.RunState.Players.Count > 1)
                return;

            // 双保险：只有"这个事件真的属于本地玩家"时才动本地界面。
            if (!LocalContext.IsMe(owner))
                return;

            await Cmd.CustomScaledWait(0.35f, 0.5f);
            await NEventRoom.Proceed();
        }
        catch (System.Exception e)
        {
            // 最坏结果只是"没自动离开"（玩家仍可点「继续」）⇒ 异常绝不能冒出去打断事件流程。
            Entry.Logger.Warn($"[EventFlow] auto-proceed skipped: {e.Message}");
        }
    }
}
