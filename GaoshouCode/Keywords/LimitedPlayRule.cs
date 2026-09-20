using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib.Interop.AutoRegistration;
using MegaCrit.Sts2.Core.Combat;

namespace Gaoshou.Keywords;

/// <summary>
/// 「限制」词条的规则：**每回合每名玩家最多只能打出 1 张带该词条的牌**。
///
/// 实现（按用户的方案）：**每回合一个标记**——
///   * 本回合打出过限制牌 → 标记 = 已用掉；
///   * 下回合开始 → 标记恢复可用。
/// 这里把标记与**回合号**绑在一起（<c>PlayerCombatState.TurnNumber</c>）：只要回合号变了，
/// 就当成"新回合、标记已重置"，因此**不需要额外的回合开始钩子**，也不会漏掉任何重置时机。
///
/// 性能：判定是"查一次字典 + 比一个回合号"，O(1)；标记只在**真的打出一张限制牌时**被写入一次
/// （<see cref="BeforeCardPlayed" />，战斗钩子是全网同步执行的 → 两端状态一致，不会分歧）。
/// 早先那版每被问一次就扫一遍战斗历史，会随出牌数量越来越慢（玩家实测"战斗中越往后越卡"），已废弃。
/// </summary>
[RegisterSingleton]
public sealed class LimitedPlayRule : SingletonModel
{
    /// <summary>
    /// 玩家 NetId → (写入时的回合号, 该回合是否已经用掉限制额度)。
    /// 用 **NetId** 而不是 Player 模型对象取键：联机下更稳（免疫模型实例重建/克隆这类边界），
    /// 也与本模组既有约定一致（GaoshouFlowTracker 就是按 Owner.NetId 分键）。
    /// </summary>
    private static readonly Dictionary<ulong, (int TurnNumber, bool Used)> State = new();

    public LimitedPlayRule()
    {
        ModHelper.SubscribeForCombatStateHooks(Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override bool ShouldReceiveCombatHooks => true;

    /// <summary>战斗开始清空标记（新战斗不允许沿用上一场的"已用掉"）。</summary>
    public override Task BeforeCombatStart()
    {
        State.Clear();
        return Task.CompletedTask;
    }

    /// <summary>打出带「限制」的牌时，把本回合的额度标记为已用掉。</summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var card = cardPlay?.Card;
        var player = cardPlay?.Player;
        if (card == null || player == null)
            return Task.CompletedTask;

        if (card.Keywords.Contains(GaoshouKeyword.Limited))
            State[Key(player)] = (CurrentTurn(player), true);

        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        // 绝大多数牌没有「限制」词条：第一句就返回，等于零开销。
        if (!card.Keywords.Contains(GaoshouKeyword.Limited))
            return true;

        var player = card.Owner;
        if (player == null)
            return true;

        // 没记录 / 回合号不同 → 视为新回合（标记已重置）→ 允许打出。
        if (!State.TryGetValue(Key(player), out var state) || state.TurnNumber != CurrentTurn(player))
            return true;

        return !state.Used;
    }

    /// <summary>玩家分键（用 NetId，联机安全）。</summary>
    private static ulong Key(Player player) => player.NetId;

    private static int CurrentTurn(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
