using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using Gaoshou.Tutorial;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 关键词教程的触发点：**卡牌奖励**里出现了带「流转 / 奇迹 / 风暴」的牌 → 讲解一次
/// （2026-10-08 用户要求；原版同款场景：<c>NPowerCardFtue</c> 就是"奖励里出现能力牌就讲解一次"）。
///
/// 为什么挂在 <c>NCardRewardSelectionScreen.AfterOverlayOpened</c>：
///   * 这是**公开**的覆盖层回调，由 <c>NOverlayStack.Push</c> 在 <c>AddChildSafely</c>（= 已跑完 _Ready 与
///     刷新卡牌行）之后立刻调用 ⇒ 此刻 <c>UI/CardRow</c> 里已经有那几张奖励牌 ✓（路径与原版 _Ready 里一致 ✓）；
///   * 比挂私有的 <c>PowerCardFtueCheck</c> / <c>RefreshOptions</c> 更稳（那两个是私有方法，改名就失效 ✗）。
///
/// 只读地取一遍这一屏的牌，交给 <see cref="GaoshouTutorial.TryShowForCardReward" /> 决定讲哪几个关键词
/// （进度记在模组自己的设置里；一屏里出现多个新关键词时会合并成一个面板按页翻 ⇒ 不会叠窗 ✗）。
/// 纯本地 UI，不碰任何同步状态 ✓。
/// </summary>
public sealed class CardRewardKeywordTutorialPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_card_reward_keyword_tutorial";

    public static string Description =>
        "explain Flow / Miracle / Storm once, when a card reward offers such a card";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NCardRewardSelectionScreen), "AfterOverlayOpened"),
    ];

    public static void Postfix(NCardRewardSelectionScreen __instance)
    {
        try
        {
            // 卡牌行：原版 _Ready 里就是 GetNode<Control>("UI/CardRow") —— 同一个路径 ✓
            if (__instance.GetNodeOrNull<Control>("UI/CardRow") is not { } row)
                return;

            var offered = new List<GaoshouTutorial.KeywordCardOffer>();
            IRunState? runState = null;
            foreach (var child in row.GetChildren())
            {
                if (child is not NGridCardHolder holder)
                    continue;

                var card = holder.CardModel;
                if (card == null)
                    continue;

                // 把**节点**也带上：教程要像原版能力牌 FTUE 那样把它抬到暗色遮罩之上 ✓
                offered.Add(new GaoshouTutorial.KeywordCardOffer(card, holder));
                runState ??= card.Owner?.RunState;   // 用来判定"这一端是不是高手玩家"
            }

            if (offered.Count == 0 || !GaoshouTutorial.CanShowRewardTutorial(runState))
                return;

            GaoshouTutorial.TryShowForCardReward(offered);
        }
        catch (Exception e)
        {
            // 教程是纯粹的锦上添花：任何失败都吞掉，绝不能影响选牌界面 ✓
            Entry.Logger.Warn($"[KeywordTutorial] reward hook skipped after an error: {e.Message}");
        }
    }
}
