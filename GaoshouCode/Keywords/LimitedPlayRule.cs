using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using MegaCrit.Sts2.Core.Combat;

namespace Gaoshou.Keywords;

/// <summary>
/// 「限制」词条的规则：**每回合每张"同名"限制牌最多打出 1 张**（不同名的限制牌**不再互卡**）。
///
/// 【2026-10-05 规则修改】旧规则是"每回合每名玩家最多打出 1 张带该词条的牌"（**全局**额度）——
/// 代价由所有限制工具共同承担：手里同时有腾挪与伺机待发时，打出其中一张就锁死另一张，
/// 对"多带工具"的混抓伤害最大，且玩家的体感是"第二张工具莫名其妙不能打"（像 bug）。
/// 现在改成**按卡牌 ID 分名计数**：同名的第 2 张仍然会被挡（保留防循环作用），不同名的互不影响 ✓
///
/// 实现：**每回合一个集合**——玩家 NetId → (写入时的回合号, 本回合已打出的限制牌 ID 集合)。
///   * 只是把集合换成按 ID 记账，仍然是"查一次字典"，O(1)；
///   * 集合只在本回合真的打出限制牌时写入（<see cref="BeforeCardPlayed" />，战斗钩子全网同步执行）；
///   * 回合号变了就视为新回合（集合整体丢弃），因此**不需要额外的回合开始钩子**。
/// 「累坏了」提示 buff 已**停用**（新规则下"已经打过某张限制牌"不再等于"本回合不能再打限制牌"，
/// 挂着它反而误导；<see cref="AfterSideTurnStart" /> 里的移除保留，用来清理旧存档遗留的实例）。
/// </summary>
[RegisterSingleton]
public sealed class LimitedPlayRule : SingletonModel
{
    /// <summary>
    /// 玩家 NetId → (写入时的回合号, 该回合已打出的限制牌 ID 集合)。
    /// 用 **NetId** 而不是 Player 模型对象取键：联机下更稳（免疫模型实例重建/克隆这类边界），
    /// 也与本模组既有约定一致（GaoshouFlowTracker 就是按 Owner.NetId 分键）。
    /// </summary>
    private static readonly Dictionary<ulong, (int TurnNumber, HashSet<string> UsedIds)> State = new();

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

    /// <summary>
    /// 打出带「限制」的牌时：把**这张牌的 ID**记进本回合已打出集合。
    /// （不再挂「累坏了」buff —— 新规则下其余限制牌仍然可打，挂着它反而误导。）
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var card = cardPlay?.Card;
        var player = cardPlay?.Player;
        if (card == null || player == null)
            return Task.CompletedTask;

        if (!card.Keywords.Contains(GaoshouKeyword.Limited))
            return Task.CompletedTask;

        var playerKey = Key(player);
        var turn = CurrentTurn(player);
        if (!State.TryGetValue(playerKey, out var state) || state.TurnNumber != turn)
        {
            // 新回合（或本场还没记过）：换成一个空集合再记账。
            state = (turn, new HashSet<string>());
            State[playerKey] = state;
        }

        // 元组里的 HashSet 是引用类型 ⇒ 直接 Add 即可，无需回写字典。
        state.UsedIds.Add(LimitedKey(card));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 你的下个回合开始时移除「累坏了」：与额度重置同一时机
    /// （额度是按"回合号变了就算重置"判定的，见 <see cref="ShouldPlay" />）。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        foreach (var creature in participants)
        {
            if (creature.IsPlayer)
                await PowerCmd.Remove<TiredOutPower>(creature);
        }
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        // 绝大多数牌没有「限制」词条：第一句就返回，等于零开销。
        if (!card.Keywords.Contains(GaoshouKeyword.Limited))
            return true;

        var player = card.Owner;
        if (player == null)
            return true;

        // 没记录 / 回合号不同 → 视为新回合（集合已重置）→ 允许打出。
        if (!State.TryGetValue(Key(player), out var state) || state.TurnNumber != CurrentTurn(player))
            return true;

        // 只有**同名**（同一个卡牌 ID）本回合已打出过才挡；不同名的限制牌互不影响 ✓
        return !state.UsedIds.Contains(LimitedKey(card));
    }

    /// <summary>玩家分键（用 NetId，联机安全）。</summary>
    private static ulong Key(Player player) => player.NetId;

    /// <summary>
    /// 限制牌的"同名"分键：卡牌 ID（<c>CardModel.Id.Entry</c>）。
    /// 用 ID 而不是本地化名：改文案/换语言都不会影响规则 ✓；两张同名牌共用同一额度 ✓
    /// </summary>
    private static string LimitedKey(CardModel card) => card.Id?.Entry ?? card.GetType().Name;

    private static int CurrentTurn(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
