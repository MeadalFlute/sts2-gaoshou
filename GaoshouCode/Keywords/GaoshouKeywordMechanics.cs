using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using Gaoshou.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Keywords;

// 词条机制共享助手。增幅：弃置最多 n 张后重放 x 次。
//
// 幻影（临时复制品）的实现**不在这里**：早先的 AddPhantomCopyAsync 已被 PhantomSingleton
// （Keywords\Phantom.cs，即 1.1.4 里的 PhantomSingleton.cs）取代，且全仓库无调用者，故删除。
// 幻影复制品的单色分配统一在 PhantomSingleton 里用**同步 RNG**完成
// （card.Owner.RunState.Rng.CombatCardGeneration），不要再用本地 Random.Shared。
public static class GaoshouKeywordMechanics
{
    /// <summary>
    /// 增幅：弃置手牌中最多 <paramref name="maxDiscard" /> 张，然后按实际弃置数重放本卡效果
    /// （通过 <paramref name="mainOnce" /> 重放主效果；纯代码重放，多人两端一致，避免 BaseReplayCount 反序列化分歧）。
    /// </summary>
    public static async Task AmplifyAsync(
        this ModCardTemplate card,
        PlayerChoiceContext choiceContext,
        int maxDiscard,
        Func<PlayerChoiceContext, Task> mainOnce)
    {
        if (maxDiscard <= 0)
            return;

        var hand = PileType.Hand.GetPile(card.Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        var prompt = new LocString("cards", "GAOSHOU_AMPLIFY_PROMPT");
        // 【2026-10-06 用户决定】提示行**只标"最多能弃几张"**即可，不再强调增幅档位 ✗
        //   （中途曾加过 `（增幅 {AmplifyCount}）`，用户看过后认为多余 ⇒ 已撤掉，只留 {MaxCount}）。
        //   注意 {MaxCount} 是 min(增幅层数, 手牌数)：手牌少时数字会跟着变小，这是**刻意的**——
        //   它表示"本次真的能选几张"，不是卡面档位；玩家若疑惑档位请直接看卡面「增幅N」✓
        prompt.Add("MaxCount", maxDiscard);
        var prefs = new CardSelectorPrefs(prompt, 0, Math.Min(maxDiscard, hand.Count));
        // ⚠️⚠️ 这里**绝不能**再自己赋值 `prefs.ShouldGlowGold = <lambda>` ✗✗（2026-09-30 正式版事故根因）
        //   反编译日志铁证（godot.log）：
        //     ERROR: System.ArgumentException: Value does not fall within the expected range.
        //       at MonoMod.Core.Interop.CoreCLR.V60.InvokeCompileMethod(...)
        //       at MonoMod.Core.Platforms.Runtimes.Core60Runtime.JitHookDelegateHolder.CompileMethodHook(...)
        //       at Gaoshou.Keywords.GaoshouKeywordMechanics.<>c.<AmplifyAsync>b__0_0(CardModel candidate)
        //       at MegaCrit.Sts2.Core.Nodes.Cards.Holders.NHandCardHolder.get_ShouldGlowGold()
        //       at ...NHandCardHolder.UpdateCard_Patch3(...)   ← 首次进入补丁化方法时触发
        //   ⇒ 异常来自 **MonoMod 的 JIT hook 在编译我们这个 lambda 时失败**，**不是** lambda 体内的逻辑
        //     ⇒ 它在 **JIT 期**抛出，所以 lambda 里的 try/catch **根本来不及执行** ✗（这解释了"兜底无效"）。
        //   ⇒ 后果链：谓词抛 → NHandCardHolder.UpdateCard 抛 → SelectCards 在
        //     RefreshSelectModeConfirmButton() 之前中断 ⇒ 确认键保持 Disabled / 选牌界面卡住
        //     ⇒ 玩家再点确认 → 原版 TaskCompletionSource 被二次完成（SetResult）→
        //       "An attempt was made to transition a task to a final state when it had already completed."
        //     ⇒ 表现为"增幅弃牌无法确认 + 牌像被复制" ✓
        //   ⇒ 修法：**完全不注入我们自己的委托**，改用原版 `CardSelectCmd.FromHandForDiscard`
        //     （奇巧高光规则由**游戏程序集内部**的谓词提供，不经过我们的 lambda）✓
        //     代价：失去"呼吸 Breath 也显示同款金色提示"这条**纯观感**加成（弃置机制本身不受影响）✓
        // 【一次性取证】入口埋点：若一次出牌里出现多行 begin（同 #diag 计数上限 3 次调用）即说明流程被跑了多次。
        var diag = NextDiag();
        if (diag > 0)
            Diag($"[AmplifyDiag] #{diag} begin card={card.Id?.Entry} maxDiscard={maxDiscard} hand={hand.Count} " +
                 $"prefs={prefs.MinSelect}..{prefs.MaxSelect}");
        var discarded = (await CardSelectCmd.FromHandForDiscard(choiceContext, card.Owner, prefs, null, card)).ToList();
        if (diag > 0)
            Diag($"[AmplifyDiag] #{diag} selected={discarded.Count} " +
                 $"[{string.Join(", ", discarded.Select(c => $"{c.Id?.Entry}#{c.GetHashCode()}:{c.Pile?.Type}"))}]");
        // 弃置：① 去掉重复项；② 只弃置**此刻仍在手牌里**的牌（被别处移走/已弃置的不再重复弃置）。
        // 这两条都是纯防御：正常路径下 Selected 里每张都恰好在手牌且唯一 ⇒ 行为与旧版逐字一致 ✓；
        // 但在"选牌返回值异常/与游戏版本行为不一致"时，可以避免把同一张牌弃置两次造成的**卡牌复制**假象。
        var discardedCount = 0;
        foreach (var c in discarded.Distinct())
        {
            if (c.Pile?.Type != PileType.Hand)
                continue;

            await CardCmd.Discard(choiceContext, c);
            discardedCount++;
        }

        for (var i = 0; i < discardedCount; i++)
            await mainOnce(choiceContext);

        if (diag > 0)
            Diag($"[AmplifyDiag] #{diag} discarded={discardedCount} replays={discardedCount}");
    }

    private static int _diagCount;

    /// <summary>一次性埋点开关：只放行前 3 次增幅流程（0 = 本次不再记录）。</summary>
    private static int NextDiag() => _diagCount < 3 ? ++_diagCount : 0;

    /// <summary>埋点输出：任何异常都不允许影响增幅流程/选牌界面。</summary>
    private static void Diag(string message)
    {
        try
        {
            Entry.Logger.Info(message);
        }
        catch (System.Exception)
        {
            // 埋点失败无所谓
        }
    }

    /// <summary>
    /// 高光是否允许出现在当前界面：只允许出现在"正常手牌行"里（出牌阶段、没有任何选牌界面）。
    /// 两类界面必须让位（2026-10-02 修复"呼吸/醉拳高光在非弃牌选牌界面误亮"）：
    ///   ⓐ **覆盖层屏幕**（<c>NOverlayStack</c>）：从牌堆选牌（头槌 / 宇宙冷漠）、查看牌堆、
    ///      卡牌奖励、遗物/卡牌详查等都由它承载 ⇒ 此时手牌还在场，但已不是"正常手牌行"。
    ///      原版自己也用同一判据（<c>NPlayerHand.UpdateCard</c> 里的
    ///      <c>NOverlayStack.Instance.ScreenCount &gt; 0</c>）⇒ 与原生口径一致。
    ///   ⓑ **手牌选择模式**（弃牌 / 消耗 / 升级 / 放回牌堆顶）：卡牌**仍然留在手牌堆里**
    ///      （<c>PileType.Hand</c>），单靠牌堆判定挡不住 ⇒ 用原版自己的
    ///      <c>NPlayerHand.IsInCardSelection</c>（<c>Mode.SimpleSelect</c> / <c>Mode.UpgradeSelect</c>）让位。
    /// 房间/手牌节点还没建好时（<c>NCombatRoom.Instance</c> 为空）按"允许"处理，行为与旧版一致。
    /// </summary>
    public static bool IsHandGlowAllowed()
    {
        // 这个闸门是在**原版 UI 回调**里被调用的（手牌高光 / 词条悬浮提示）。
        // 一旦它自己抛异常，异常会从 UI 回调冒出去、打断整条提示链（历史事故症状：
        // 所有卡牌的 hovertip 错位到屏幕左上角）⇒ 任何失败都按"允许高光"处理，
        // 最多多亮一次，绝不影响布局与流程。
        try
        {
            // ⓪ 当前"活动屏幕"必须就是战斗房间本身：一旦被任何覆盖层（从牌堆选牌 / 查看牌堆 /
            //    卡牌奖励 / 详查）顶掉，就说明玩家已不在正常出牌界面 ⇒ 一律不高光。
            //    判据与参数与原版 NPingButton / NEndTurnButton 完全一致（比的是 NCombatRoom.Instance 同一实例）。
            var room = NCombatRoom.Instance;
            if (room != null && !ActiveScreenContext.Instance.IsCurrent(room))
                return false;

            // ① 双保险：NOverlayStack 上挂了任何覆盖层屏幕（原版 NPlayerHand.UpdateCard 用的就是这个判据）。
            var overlays = NOverlayStack.Instance;
            if (overlays != null && overlays.ScreenCount > 0)
                return false;

            // ② 手牌选择模式（弃牌 / 消耗 / 升级 / 放回牌堆顶）下手牌仍留在 Hand 堆里 ⇒ 让位。
            var hand = room?.Ui?.Hand;
            return hand == null || !hand.IsInCardSelection;
        }
        catch (System.Exception e)
        {
            if (!_glowGateWarned)
            {
                _glowGateWarned = true;
                Entry.Logger.Warn($"[GlowGate] hand-selection probe failed, allowing glow: {e.Message}");
            }
            return true;
        }
    }

    private static bool _glowGateWarned;
}
