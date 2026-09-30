using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Rooms;
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
        prompt.Add("MaxCount", maxDiscard);
        var prefs = new CardSelectorPrefs(prompt, 0, Math.Min(maxDiscard, hand.Count));
        // 复刻原版 FromHandForDiscard 的临时高光规则：只有真正的奇巧牌可亮。
        // 呼吸不是奇巧牌，但其弃置效果会在此处触发，因此额外显示同款提示。
        //
        // ⚠️ 这个谓词是**全局唯一**由"原版手牌选牌 UI 在构建选牌界面时逐张调用"的模组代码：
        //    NPlayerHand.UpdateSelectModeCardVisibility → NHandCardHolder.UpdateCard → ShouldGlowGold
        //    → NPlayerHand.SelectModeGoldGlowOverride → 就是这里 ✗。
        //    它一旦抛异常，SelectCards 会在 RefreshSelectModeConfirmButton() **之前**中断
        //    ⇒ 确认键永远处于 Disabled ⇒ 症状就是"增幅弃牌的选牌界面无法确认"
        //    （2026-09-30 正式版实测；beta 正常 —— 同一个谓词在两个游戏版本里的返回值/异常行为可以不同）。
        //    ⇒ 因此这里做 **全量兜底**：任何异常都当作"不亮"，绝不把异常抛回原版 UI。
        prefs.ShouldGlowGold = candidate =>
        {
            try
            {
                if (candidate is Breath)
                    return true;
                if (!candidate.IsSlyThisTurn)
                    return false;

                return candidate.CanPlay(out var reason, out _) || reason.HasResourceCostReason();
            }
            catch (System.Exception)
            {
                return false;
            }
        };
        var discarded = (await CardSelectCmd.FromHand(choiceContext, card.Owner, prefs, null, card)).ToList();
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
    }

    /// <summary>
    /// 高光是否允许出现在当前界面：只允许出现在"正常手牌行"里。
    /// 手牌选择模式（弃牌 / 选牌界面）下卡牌**仍然留在手牌堆里**（<c>PileType.Hand</c>），
    /// 单靠牌堆判定挡不住；且原版此时会用 <c>CardSelectorPrefs.ShouldGlowGold</c>
    /// （经 <c>NPlayerHand.SelectModeGoldGlowOverride</c>）接管泛光判定 ⇒ 模组必须自己让位，
    /// 否则改色后的流转/奇迹/风暴高光会照到选牌界面上。
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
            var hand = NCombatRoom.Instance?.Ui?.Hand;
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
