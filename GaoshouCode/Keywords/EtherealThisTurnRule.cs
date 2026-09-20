using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

/// <summary>
/// 「本回合内额外具有虚无」的收尾规则（没想好 Fatal Revealation 用）。
///
/// <c>CardModel.AddKeyword(Ethereal)</c> 加上的词条是**永久**的：如果不在回合结束时摘掉，
/// 被抽到的那张牌下回合会继续带着「虚无」并在回合末被消耗掉。所以这里按玩家记录
/// "本回合被我们加过虚无的牌"，在该玩家回合结束时逐张摘掉。
///
/// 只记录**原本没有虚无**的牌 —— 本来就有虚无的牌（例如速子长矛）不能被我们摘掉词条。
/// 分键用 NetId（联机安全，与本模组既有规则一致：LimitedPlayRule / GaoshouFlowTracker 同款），
/// 状态在战斗开始时清空。
/// </summary>
[RegisterSingleton]
public sealed class EtherealThisTurnRule : SingletonModel
{
    /// <summary>玩家 NetId → 本回合被我们加过「虚无」的牌。</summary>
    private static readonly Dictionary<ulong, List<CardModel>> Marked = new();

    public EtherealThisTurnRule()
    {
        ModHelper.SubscribeForCombatStateHooks(Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override bool ShouldReceiveCombatHooks => true;

    /// <summary>记录"本回合被我们加过虚无"的牌（调用方须先确认这些牌原本没有虚无）。</summary>
    public static void Mark(Player player, IEnumerable<CardModel> cards)
    {
        if (!Marked.TryGetValue(player.NetId, out var list))
            Marked[player.NetId] = list = [];

        foreach (var card in cards)
        {
            if (!list.Contains(card))
                list.Add(card);
        }
    }

    /// <summary>新战斗不沿用上一场的标记。</summary>
    public override Task BeforeCombatStart()
    {
        Marked.Clear();
        return Task.CompletedTask;
    }

    /// <summary>该玩家回合结束：摘掉我们加上的虚无（只摘我们加的那些）。</summary>
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        foreach (var creature in participants)
        {
            var player = creature.Player;
            if (player == null || !Marked.Remove(player.NetId, out var cards))
                continue;

            foreach (var card in cards)
                CardCmd.RemoveKeyword(card, CardKeyword.Ethereal);
        }

        return Task.CompletedTask;
    }
}
