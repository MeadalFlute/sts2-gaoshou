using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

// 奇迹计数器：统一奇迹就绪判定 + 本场奇迹触发计数。
// 奇迹就绪 = 奇迹牌（带奇迹词条）不是"本回合开始时抽牌"进入手牌。
// 用一个"回合初抽牌池子"(_turnStartDrawn) 记录本回合初抽进手的奇迹牌；
// 任何不在池子里的奇迹卡（借用之牌/幻影复制品、手牌复制、丢弃后进手、上一回合保留/囤积遗留等）
// 都视为"非回合初进手" → 奇迹就绪。clone 是独立引用实例，天然不在池子里，无需特殊处理。
// 回合结束清空池子：保留/囤积带至下一轮的卡，下一轮重新成为"非回合初"。
// 本场奇迹触发次数(_counts)按玩家分键，供速子长矛等读取。
[RegisterSingleton]
public sealed class MiracleCounter : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    private static readonly Dictionary<Player, int> _counts = new();
    private static readonly HashSet<CardModel> _turnStartDrawn = new();

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
        _turnStartDrawn.Clear();
        return Task.CompletedTask;
    }

    // 抽牌路径：回合开始时抽到的奇迹牌 → 加入"回合初抽牌池子"。
    // fromHandDraw=true 表示这是回合初从抽牌堆抽到手的（奇迹判定用）。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (fromHandDraw && card.Owner != null && card.Keywords.Contains(GaoshouKeyword.Miracle))
        {
            _turnStartDrawn.Add(card);
        }
        return Task.CompletedTask;
    }

    // 回合结束时清空"回合初抽牌池子"：保留/囤积带至下一轮的奇迹牌，下一轮重新视为"非回合初"。
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        _turnStartDrawn.Clear();
        return Task.CompletedTask;
    }

    // 奇迹牌被打出且奇迹就绪（非回合初抽牌进手）→ 计一次奇迹（按玩家分键）。
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner == null || card.Owner != cardPlay.Player)
            return Task.CompletedTask;
        if (!card.Keywords.Contains(GaoshouKeyword.Miracle))
            return Task.CompletedTask;
        if (IsMiracleReady(card))
        {
            _counts[card.Owner] = _counts.GetValueOrDefault(card.Owner) + 1;
        }
        return Task.CompletedTask;
    }

    // 静态读取（与钩子实例解耦；按玩家）。
    public static int GetMiracleCount(Player player)
    {
        return _counts.GetValueOrDefault(player);
    }

    // 奇迹就绪（统一来源）：奇迹牌且本回合初抽牌池子里没有它。
    public static bool IsMiracleReady(CardModel? card)
    {
        if (card == null)
            return false;
        if (!card.Keywords.Contains(GaoshouKeyword.Miracle))
            return false;
        return !_turnStartDrawn.Contains(card);
    }
}
