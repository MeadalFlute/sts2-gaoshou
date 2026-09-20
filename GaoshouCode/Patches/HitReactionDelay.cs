using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 「挨打之后」类原版能力的结算时机兼容层（目前只涉及花园幽灵鳗的「胆小 Skittish」）。
///
/// 原版多段攻击是**一条 AttackCommand 打 N 下**：<c>Hook.AfterAttack</c> 只在整条指令打完之后触发一次，
/// 所以胆小这类"第一次挨打后获得格挡"的能力，原版表现是**全部伤害结算完**才发格挡
/// （反编译：SkittishPower.cs:56 挂在 AfterAttack；Hook.AfterAttack 只有两个调用点 ——
/// AttackCommand.cs:672 与 AttackContext.cs:74）。
///
/// 我们的多段卡是**每段一条 AttackCommand**（DoalBian / LinkedStrike / TigerClaw / PerfectStickSword /
/// TachyonLance），于是这类反应会在第一段之后就结算，后面的段数全被格挡吃掉。
///
/// 修法：这几张卡在开始结算伤害前 <see cref="Begin" /> 打开一个"延迟窗口"，
/// 窗口内把 <see cref="SkittishPower" /> 的 AfterAttack 压住并记账，等**全部段数**打完后
/// 在窗口 Dispose 时按顺序补触发（直接调用原版方法本体，不复制它的判定）。
/// 窗口期间伤害数值与活力补偿保持原样，只是这条反应整体后移。
///
/// 注意：「蜷身 CurlUp」挂在 <c>AfterCardPlayed</c>（出牌结束）而非 AfterAttack，
/// 单次出牌内的多段伤害本来就在它之前全部结算完，同一张牌被重复打出时也按原版节奏处理 —— 所以不在这里动它。
/// </summary>
internal static class HitReactionDelay
{
    private const string LogTag = "[HitDelay]";

    private static readonly List<(SkittishPower Power, AttackCommand Command)> SkittishPending = [];

    private static int _depth;

    /// <summary>当前是否处于"我们的多段卡正在结算伤害"的窗口内。</summary>
    internal static bool Active => _depth > 0;

    /// <summary>
    /// 打开延迟窗口：作用域内 Skittish 的挨打反应被压住，作用域结束时统一补触发。
    /// </summary>
    internal static Scope Begin(PlayerChoiceContext choiceContext)
    {
        _depth++;
        return new Scope(choiceContext);
    }

    /// <summary>把一次 Skittish 挨打反应记账并压住（由补丁前缀调用）。</summary>
    internal static void DeferSkittish(SkittishPower power, AttackCommand command)
    {
        SkittishPending.Add((power, command));
    }

    private static async Task FlushAsync(PlayerChoiceContext choiceContext)
    {
        if (SkittishPending.Count == 0)
            return;

        var pending = SkittishPending.ToArray();
        SkittishPending.Clear();

        foreach (var (power, command) in pending)
        {
            // 战斗已结束 / 敌人已死 / 能力已被移除 → 不再补触发（原版这种情况下也不会真的发格挡）。
            if (CombatManager.Instance.IsEnding || !power.Owner.IsAlive || !power.Owner.Powers.Contains(power))
                continue;

            try
            {
                await power.AfterAttack(choiceContext, command);
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"{LogTag} deferred Skittish reaction failed: {ex.Message}");
            }
        }
    }

    internal sealed class Scope(PlayerChoiceContext choiceContext) : IAsyncDisposable
    {
        private bool _disposed;

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_depth > 0)
                _depth--;

            // 只有最外层窗口（这张牌全部伤害段都打完）才补触发。
            if (_depth == 0)
                await FlushAsync(choiceContext);
        }
    }
}

/// <summary>
/// 压住 <see cref="SkittishPower.AfterAttack" />：只在我们的多段卡伤害结算窗口内生效，
/// 窗口外（原版卡、其它 mod）行为完全不变。
/// </summary>
public sealed class SkittishReactionDelayPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_skittish_reaction_delay";

    public static string Description => "defer Skittish block gain until a multi-segment card has dealt all its damage";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method(
            typeof(SkittishPower),
            nameof(SkittishPower.AfterAttack),
            typeof(PlayerChoiceContext),
            typeof(AttackCommand)),
    ];

    public static bool Prefix(SkittishPower __instance, AttackCommand command, ref Task __result)
    {
        if (!HitReactionDelay.Active)
            return true;

        HitReactionDelay.DeferSkittish(__instance, command);
        __result = Task.CompletedTask;
        return false;
    }
}
