using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Characters;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 走马灯（罕见）：每回合你打出第 3 张临时牌时，获得 1 能量、1 星辉（每回合仅触发一次）。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class Carousel : ModRelicTemplate
{
    private int _tempPlayedThisTurn;
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool ShowCounter => MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress;

    public override int DisplayAmount => _tempPlayedThisTurn;

    public override bool ShouldReceiveCombatHooks => true;

        protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];


public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            _tempPlayedThisTurn = 0;
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        if (player != Owner || _triggeredThisTurn)
            return;
        if (cardPlay.Card.DeckVersion != null)
            return;

        _tempPlayedThisTurn++;
        InvokeDisplayAmountChanged();
        if (_tempPlayedThisTurn < 3)
            return;

        _triggeredThisTurn = true;
        InvokeDisplayAmountChanged();
        // 先给星辉（受 ShouldGainStars 门控，打点其被判否的情况）再给能量。
        await PlayerCmd.GainStars(1, player);
        await PlayerCmd.GainEnergy(1, player);
    }
}