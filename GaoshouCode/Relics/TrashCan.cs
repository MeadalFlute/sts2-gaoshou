using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class TrashCan : ModRelicTemplate
{
    private const int TurnsThreshold = 3;
    private int _turnsSeen;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => TurnsSeen;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new[] { new DynamicVar("Turns", TurnsThreshold) };

    [SavedProperty]
    public int TurnsSeen
    {
        get => _turnsSeen;
        set
        {
            AssertMutable();
            _turnsSeen = value;
            InvokeDisplayAmountChanged();
        }
    }

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature))
            return;

        TurnsSeen = (TurnsSeen + 1) % TurnsThreshold;
        if (TurnsSeen != 0)
            return;

        Flash();
        var player = Owner;
        var candidates = ModelDb.AllCardPools
            .SelectMany(p => p.GetUnlockedCards(
                player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c is Gaoshou.Keywords.IWasteCard)
            .ToList();
        var cards = CardFactory.GetDistinctForCombat(
            player, candidates, 1, player.RunState.Rng.CombatCardGeneration).ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, player);
    }
}