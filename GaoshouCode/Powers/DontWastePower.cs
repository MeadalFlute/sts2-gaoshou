using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using Gaoshou.Keywords;

namespace Gaoshou.Powers;

// 可别浪费（能力）：你的回合开始时，获得 Amount 张随机「废品牌」。
// 生成方式与临时武器一致（CardFactory.GetDistinctForCombat + AddGeneratedCardToCombat）。
[RegisterPower]
public sealed class DontWastePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/dontwaste.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/dontwaste.png");

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side)
            return;
        // 多人：只在自己回合开始时结算（否则对方回合也会给本机加废品牌）。
        if (!participants.Contains(Owner))
            return;

        var player = Owner.Player;
        if (player == null || Amount <= 0)
            return;

        var candidates = ModelDb.AllCardPools
                .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                .Where(c => c is IWasteCard)
                .ToList();
        var generated = CardFactory.GetDistinctForCombat(
                player,
                candidates,
                (int)Amount,
                player.RunState.Rng.CombatCardGeneration)
            .ToList();
        foreach (var c in generated)
            await CardPileCmd.AddGeneratedCardToCombat(c, PileType.Hand, player);
    }
}