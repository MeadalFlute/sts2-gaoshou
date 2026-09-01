using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

// 奇迹计数器：本场战斗中"奇迹牌（带奇迹词条）以非回合开始时抽牌的方式进入手牌、随后被打出"的次数。
// 计数挂在 STATIC 表（_counts 静态）：无论钩子实例与读取方是否同一对象，数据同源。
// 按玩家分键（字典 key = Player），多人各计各的，绝不混加。
[RegisterSingleton]
public sealed class MiracleCounter : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    private static readonly Dictionary<Player, int> _counts = new();
    private static readonly HashSet<CardModel> _enteredNonTurnStart = new();

    public MiracleCounter()
    {
        ModHelper.SubscribeForCombatStateHooks(base.Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override Task BeforeCombatStart()
    {
        _counts.Clear();
        _enteredNonTurnStart.Clear();
        return Task.CompletedTask;
    }

    // 回合结束时仍留在手牌的奇迹牌（保留/囤积带至下一轮）：登记为"非回合开始时抽牌"进入。
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        foreach (var creature in participants)
        {
            if (creature.Player == null)
                continue;
            var hand = PileType.Hand.GetPile(creature.Player)?.Cards;
            if (hand == null)
                continue;
            foreach (var card in hand)
            {
                if (card.Owner == creature.Player && card.Keywords.Contains(GaoshouKeyword.Miracle))
                {
                    _enteredNonTurnStart.Add(card);
                }
            }
        }
        return Task.CompletedTask;
    }

    // 抽牌路径：非回合开始抽牌进入手牌 → 登记。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!fromHandDraw && card.Owner != null)
        {
            _enteredNonTurnStart.Add(card);
        }
        return Task.CompletedTask;
    }

    // 加入路径：非来自抽牌堆进入手牌 → 登记。
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Pile?.Type == PileType.Hand && oldPileType != PileType.Hand && oldPileType != PileType.Draw && card.Owner != null)
        {
            _enteredNonTurnStart.Add(card);
        }
        return Task.CompletedTask;
    }

    // 奇迹牌被打出且此前以非回合抽牌方式进手 → 计一次奇迹（按玩家分键）。
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner == null || card.Owner != cardPlay.Player)
            return Task.CompletedTask;
        if (!card.Keywords.Contains(GaoshouKeyword.Miracle))
            return Task.CompletedTask;
        if (!_enteredNonTurnStart.Contains(card))
        {
            return Task.CompletedTask;
        }

        var player = card.Owner;
        _counts[player] = _counts.GetValueOrDefault(player) + 1;
        return Task.CompletedTask;
    }

    // 静态读取（与钩子实例解耦；按玩家）。
    public static int GetMiracleCount(Player player)
    {
        var n = _counts.GetValueOrDefault(player);
        return n;
    }
}