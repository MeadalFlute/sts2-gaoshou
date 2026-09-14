using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Gaoshou.Events;

/// <summary>
/// 事件里"本角色的初始打击 / 初始防御牌"的唯一取法。
///
/// 为什么不能用 <c>Character.CardPool.AllCards</c> + <c>Rarity == CardRarity.Basic</c> + 标签去猜：
/// 卡池的 AllCards 里混着原版共享的基础牌，<c>FirstOrDefault</c> 很可能先命中**铁甲战士的打击/防御**，
/// 于是事件发的牌和 tooltip 联想的牌都变成了别人的（玩家实测：格挡达人选项 2 联想到了铁甲战士的基础打防）。
///
/// 正确来源是 <see cref="CharacterModel.StartingDeck" />：角色真正开局带的那几张牌
/// （高手的初始打击=LinkedStrike、初始防御=LinkedBlock，由 RitsuLib 按初始牌注册生成）。
/// </summary>
internal static class CharacterBasicCards
{
    /// <summary>角色开局牌组里带指定标签（<see cref="CardTag.Strike" /> / <see cref="CardTag.Defend" />）的那张牌。</summary>
    public static CardModel? FindBasic(Player player, CardTag tag)
        => player.Character.StartingDeck.FirstOrDefault(card => card.Tags.Contains(tag));

    /// <summary>同 <see cref="FindBasic" />，但接受可空 Player（Owner 可能为空时用）。</summary>
    public static CardModel? FindBasicOrNull(Player? player, CardTag tag)
        => player == null ? null : FindBasic(player, tag);
}
