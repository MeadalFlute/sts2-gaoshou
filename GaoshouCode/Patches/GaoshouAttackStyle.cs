using Godot;
using MegaCrit.Sts2.Core.Commands.Builders;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 「这一次攻击是 AoE（打全体）」标记 —— 给动画状态机区分"横扫"与"拳"用。
///
/// 判定来源：<see cref="AttackCommand" /> 自己带目标信息（<c>IsMultiTargeted</c> = 目标取自战斗状态、
/// <c>IsRandomlyTargeted</c> = 随机多目标），在 <c>Execute</c> 的 Prefix 里读一次即可 ——
/// 触发动画的 <c>AttackCommand.cs:584 await CreatureCmd.TriggerAnim(...)</c> 就在 <c>Execute</c> 方法体内，
/// 所以 Prefix 一定早于触发器 ✓。
///
/// 【为什么要"会话"而不是每次命令都改写】2026-09-24 实机 bug：
/// 多段 AoE（醉拳 / 蓄力刀这类 <c>WithHitCount</c> 的全体攻击）**第二段开始掉回挥拳** ✗。
/// 原因：一次多段攻击内部还会**嵌套**跑别的攻击命令（反伤、亡语、追击、敌人被打死时的效果……），
/// 旧写法"每个命令都改写风格 + 任何命令结束都清标记"，会让嵌套命令把外层的"横扫"冲掉 ⇒ 后面几段按拳播。
///
/// 现在的规则：
/// <list type="number">
///   <item>**最外层**那次攻击决定风格：进入 Execute 时若已有攻击在跑（= 嵌套），**不改写**；</item>
///   <item>风格**不在结束时清除**，而是在下一次最外层攻击进入时被覆盖 ⇒ 迟到的完成续体不会误清新攻击；</item>
///   <item>会话结束用 <c>ReferenceEquals</c> 判定（只有主人自己能收尾），并有 2 秒兜底，免得续体没跑到时永久占住。</item>
/// </list>
///
/// ⚠️ 另一个坑（同一天踩的）：本模组的补丁**不是自动发现的**，必须在 <c>Entry.cs</c> 里
/// <c>patcher.RegisterPatch&lt;T&gt;()</c> 显式注册 —— 只写 IPatchMethod 类不会挂上，
/// 而且日志里 "Applying N patches" 的 N 也不变（当时 18 → 18，差点没看出来）✗。
/// </summary>
public static class GaoshouAttackStyle
{
    /// <summary>调试日志开关（日志前缀 <c>[Gaoshou][AoE]</c>）：这个 bug 确认修好后置 false 即可。</summary>
    public const bool LogDiagnostics = true;

    /// <summary>会话最长存活时间（秒）：万一完成续体没跑到，也别让会话永久占住（正常一次攻击远短于它）。</summary>
    private const double StaleSessionSeconds = 2.0;

    private static AttackCommand? _session;
    private static double _sessionStartedAt;
    private static bool _aoe;

    /// <summary>当前这次攻击是否"打全体"（纯读取，可在状态机谓词里用）。</summary>
    public static bool IsAoe => _aoe;

    /// <summary>进入 <c>AttackCommand.Execute</c>：只有最外层那次攻击才决定风格。</summary>
    public static void Enter(AttackCommand command)
    {
        // 只认玩家自己发起的攻击：敌人攻击、宠物攻击不该改写玩家动画的风格，
        // 也不该占住会话（否则玩家下一张牌会被误判成"嵌套"）。
        if (command.Attacker is not { IsPlayer: true })
            return;

        var now = Time.GetTicksMsec() / 1000.0;
        var stale = _session == null || now - _sessionStartedAt > StaleSessionSeconds;
        // 同一张牌的后续段（完美棍剑：先单体多段、再全体一段）**要**能改写风格；
        // 而出处不同的命令（反伤 / 亡语 / 追击打出来的攻击）是嵌套，不能改写。
        var samePlay = !stale && command.CardPlay != null &&
                       ReferenceEquals(command.CardPlay, _session!.CardPlay);

        if (!stale && !samePlay)
        {
            if (LogDiagnostics)
                Entry.Logger.Info(
                    $"[Gaoshou][AoE] 嵌套攻击（沿用外层风格 aoe={_aoe}）: {Describe(command)}");
            return;
        }

        _session = command;
        _sessionStartedAt = now;
        _aoe = command.IsMultiTargeted && !command.IsRandomlyTargeted;
        if (LogDiagnostics)
            Entry.Logger.Info($"[Gaoshou][AoE] 新攻击会话 aoe={_aoe}: {Describe(command)}");
    }

    /// <summary>该命令执行完（挂在 <c>__result.ContinueWith</c>）：只有会话主人能收尾，且不清风格。</summary>
    public static void Exit(AttackCommand command)
    {
        if (!ReferenceEquals(_session, command))
            return;

        _session = null;
        if (LogDiagnostics)
            Entry.Logger.Info($"[Gaoshou][AoE] 攻击会话结束（风格保留到下一次攻击开始）: {Describe(command)}");
    }

    /// <summary>
    /// 视觉回到站姿（状态机进入 idle）时收尾 —— 比 <c>ContinueWith</c> 更贴近"这一次攻击真的结束了"，
    /// 于是紧接着打出的下一张牌不会被误判成嵌套。由 GaoshouCharacter 的状态机回调。
    /// </summary>
    public static void CloseSession()
    {
        _session = null;
    }

    private static string Describe(AttackCommand c)
    {
        return $"multi={c.IsMultiTargeted} single={c.IsSingleTargeted} random={c.IsRandomlyTargeted} "
               + $"card={c.ModelSource?.GetType().Name ?? "null"} id={c.GetHashCode()}";
    }
}

/// <summary>
/// 挂 <c>AttackCommand.Execute</c>：Prefix 判定风格，Postfix 用 <c>__result.ContinueWith</c> 收尾
/// （⚠️ <c>Execute</c> 是 async，Harmony 的 Postfix 在方法**返回 Task 时**就跑，必须挂到 Task 上）。
/// </summary>
public sealed class SetAoeAttackStylePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_set_aoe_attack_style";

    public static string Description =>
        "decide whether this attack's visuals are the AoE sweep (outermost command wins, nested commands keep the style)";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(AttackCommand), nameof(AttackCommand.Execute)),
    ];

    public static void Prefix(AttackCommand __instance) => GaoshouAttackStyle.Enter(__instance);

    public static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        __result?.ContinueWith(_ => GaoshouAttackStyle.Exit(__instance));
    }
}
