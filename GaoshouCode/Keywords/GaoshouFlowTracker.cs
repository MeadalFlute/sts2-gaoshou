using System.Collections.Generic;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

// 流转（Flow）判定器：
//  - 按玩家分别记录各自上一张打出的牌的颜色（多人互不影响）。
//  - 「流转触发」= 本卡带【流转】词条，且颜色与本玩家上一张牌的颜色“完全不同”（无共同基础色）。
//  - 特例（2026-09-22 起）：本场战斗还没打出过牌时（即第一张牌）同样视为触发。
//  - 供流转卡 OnPlay 条件、流转触发类能力（枪斗术/武学宗师/剑舞）以及手牌泛光（ShouldGlowGoldInternal）使用。
[RegisterSingleton]
public sealed class GaoshouFlowTracker : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    private static readonly Dictionary<ulong, CardModel> _currentCard = new();

    private static readonly Dictionary<ulong, GaoshouCardColor> _previousColor = new();

    private static readonly Dictionary<Type, GaoshouCardColor> _colorCache = new();

    private static readonly HashSet<GaoshouCardColor>[] _baseColors =
    [
        /* Red            */ new() { GaoshouCardColor.Red },
        /* Blue           */ new() { GaoshouCardColor.Blue },
        /* Purple         */ new() { GaoshouCardColor.Purple },
        /* Green          */ new() { GaoshouCardColor.Green },
        /* Colorless      */ new() { GaoshouCardColor.Colorless },
        /* Black          */ new() { GaoshouCardColor.Black },
        /* RedBlue        */ new() { GaoshouCardColor.Red, GaoshouCardColor.Blue },
        /* RedPurple      */ new() { GaoshouCardColor.Red, GaoshouCardColor.Purple },
        /* BluePurple     */ new() { GaoshouCardColor.Blue, GaoshouCardColor.Purple },
        /* RedGreen       */ new() { GaoshouCardColor.Red, GaoshouCardColor.Green },
        /* BlueGreen      */ new() { GaoshouCardColor.Blue, GaoshouCardColor.Green },
        /* ColorlessPurple*/ new() { GaoshouCardColor.Colorless, GaoshouCardColor.Purple },
        /* ColorlessBlack */ new() { GaoshouCardColor.Colorless, GaoshouCardColor.Black },
    ];

    public GaoshouFlowTracker()
    {
        ModHelper.SubscribeForCombatStateHooks(base.Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    // 新战斗开始时清空记录（含卡引用），避免跨战斗串色与残留引用。
    public override Task BeforeCombatStart()
    {
        _currentCard.Clear();
        _previousColor.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 在**打出开始**时登记，而不是打完再登记。
    ///
    /// 2026-09-23 据 bug report 修复：旧实现挂在 AfterCardPlayedLate 上（打出结束才记颜色），
    /// 于是"上一张打出的牌"在**嵌套打出**里是错的 ——
    /// 止水（蓝）播放期间弃掉醉拳（红紫），醉拳被奇巧自动打出时会去比"止水之前的那张牌"
    /// （可能是红/紫，于是流转不触发）。现在：
    ///   * 开始打出一张牌时，把原来那张挪到"上一张"，自己成为"正在打出的牌"；
    ///   * 正在打出的牌 → 与"上一张"比（嵌套时就是外层父牌，即止水）；
    ///   * 其它情况（例如手牌泛光）→ 与"正在打出的那张"比。
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner == null)
            return Task.CompletedTask;

        var netId = card.Owner.NetId;
        if (_currentCard.TryGetValue(netId, out var previous))
            _previousColor[netId] = GetColor(previous);

        _currentCard[netId] = card;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 读取卡的 GaoshouCardColor：幻影复制品优先用实例级单色（杠式霰弹枪等双色卡幻影=随机单色，
    /// 流转判定必须按实例颜色而非类型缓存）；否则按类型缓存反射结果；无该属性的卡视为无色。
    /// </summary>
    public static GaoshouCardColor GetColor(CardModel card)
    {
        if (PhantomColorRegistry.TryGet(card, out var assigned))
            return assigned;

        var type = card.GetType();
        if (_colorCache.TryGetValue(type, out var cached))
            return cached;

        var color = GaoshouCardColor.Colorless;
        var prop = type.GetProperty("CardColor", BindingFlags.Public | BindingFlags.Instance);
        if (prop?.GetValue(card) is GaoshouCardColor value)
            color = value;

        _colorCache[type] = color;
        return color;
    }

    /// <summary>
    /// 本卡当前是否满足“流转”触发条件：带流转词条，且颜色与本玩家上一张打出牌完全不同。
    ///
    /// 2026-09-22 应玩家要求调整：**本场战斗还没打出过牌时（= 第一张牌）也视为就绪**。
    /// 2026-09-23 修复嵌套打出的比较对象（见 BeforeCardPlayed 的注释）。
    /// 注意记录是按**战斗**清空的（BeforeCombatStart），所以“第一张牌”指本场战斗的第一张，
    /// 而不是每个回合的第一张。
    /// </summary>
    public static bool IsFlowReady(CardModel card)
    {
        if (!card.Keywords.Contains(GaoshouKeyword.Flow))
            return false;
        if (card.Owner == null)
            return false;

        var netId = card.Owner.NetId;

        // 自己不是"正在打出的那张"（例如手牌泛光）：与正在打出的那张比。
        if (_currentCard.TryGetValue(netId, out var current) && current != card)
            return !SharesBaseColor(GetColor(card), GetColor(current));

        // 本玩家本场战斗还没打出过牌（= 第一张）：按“就绪”处理（2026-09-22 调整）。
        if (!_previousColor.TryGetValue(netId, out var previous))
            return true;

        return !SharesBaseColor(GetColor(card), previous);
    }

    /// <summary>
    /// 仅用于卡牌视觉高光：流转提示只应显示在手牌中，避免弃牌堆、抽牌堆或选牌界面
    /// 被误认为原版奇巧（Sly）效果。实际打出时仍使用 IsFlowReady 进行结算判定。
    /// </summary>
    public static bool IsFlowGlowReady(CardModel card)
    {
        return card.Pile?.Type == PileType.Hand && IsFlowReady(card);
    }

    private static bool SharesBaseColor(GaoshouCardColor a, GaoshouCardColor b)
    {
        var setA = _baseColors[(int)a];
        var setB = _baseColors[(int)b];
        foreach (var color in setA)
        {
            if (setB.Contains(color))
                return true;
        }
        return false;
    }
}
