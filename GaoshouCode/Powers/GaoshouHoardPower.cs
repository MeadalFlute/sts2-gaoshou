using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Hooks;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 囤积（能力）：以 buff 层数计数。回合结束时（弃牌阶段前），
// 选择最多 Amount 张手牌保留，随后层数减少实际保留的牌数；耗尽后移除。
// 参考原版「计划妥当(WellLaidPlansPower)」的 BeforeFlushLate 保留实现。
[RegisterPower]
public sealed class GaoshouHoardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/hoard.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/hoard.png");

    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0)
            return;
        if (player.Creature.CombatState is not { } combatState || !Hook.ShouldFlush(combatState, player))
            return;

        var prompt = new LocString("cards", "GAOSHOU_HOARD_PROMPT");
        prompt.Add("MaxCount", (int)Amount);
        var prefs = new CardSelectorPrefs(prompt, 0, (int)Amount);
        var selected = (await CardSelectCmd.FromHand(
                choiceContext, Owner.Player, prefs,
                (CardModel c) => !c.ShouldRetainThisTurn, this))
            .ToList();
        if (selected.Count == 0)
            return;

        foreach (var card in selected)
            card.GiveSingleTurnRetain();

        // 层数减少实际保留的牌数。
        var remaining = Amount - selected.Count;
        if (remaining <= 0)
            await PowerCmd.Remove(this);
        else
            Amount = (int)remaining;
    }
}