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
//  - 供流转卡 OnPlay 条件、流转触发类能力（枪斗术/武学宗师）以及手牌泛光（ShouldGlowGoldInternal）使用。
[RegisterSingleton]
public sealed class GaoshouFlowTracker : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    private static readonly Dictionary<ulong, GaoshouCardColor> _lastPlayedByPlayer = new();

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

    // 新战斗开始时清空上一场的颜色记录，避免跨战斗串色。
    public override Task BeforeCombatStart()
    {
        _lastPlayedByPlayer.Clear();
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        RecordPlay(cardPlay.Card);
        return Task.CompletedTask;
    }

    public static void RecordPlay(CardModel card)
    {
        if (card.Owner == null)
            return;
        var color = GetColor(card);
        _lastPlayedByPlayer[card.Owner.NetId] = color;
    }

    /// <summary>
    /// 读取卡的 GaoshouCardColor（按类型缓存反射结果；无该属性的卡视为无色）。
    /// </summary>
    public static GaoshouCardColor GetColor(CardModel card)
    {
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
    /// 本玩家尚未打出过牌时（上一张未知）不触发。
    /// </summary>
    public static bool IsFlowReady(CardModel card)
    {
        if (!card.Keywords.Contains(GaoshouKeyword.Flow))
            return false;
        if (card.Owner == null || !_lastPlayedByPlayer.TryGetValue(card.Owner.NetId, out var previous))
        {
            return false;
        }
        var ready = !SharesBaseColor(GetColor(card), previous);
        return ready;
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