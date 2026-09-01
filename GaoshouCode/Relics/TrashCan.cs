using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Characters;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using Gaoshou.Keywords;

namespace Gaoshou.Relics;

// 垃圾桶（罕见）：每 3 回合，往你的手牌中添加一张随机废品牌（计数器跨战斗累计）。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class TrashCan : ModRelicTemplate
{
    private int _turnCount;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool ShowCounter => true;

    public override int DisplayAmount => _turnCount % 3;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        _turnCount++;
        if (_turnCount % 3 != 0)
            return;

        // 往手牌添加 1 张随机废品牌（保留跨战斗计数）。
        var candidates = ModelDb.AllCardPools
                .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
                .Where(c => c is Gaoshou.Keywords.IWasteCard)
                .ToList();
        var generated = CardFactory.GetDistinctForCombat(player, candidates, 1,
                player.RunState.Rng.CombatCardGeneration).ToList();
        foreach (var c in generated)
            await CardPileCmd.AddGeneratedCardToCombat(c, PileType.Hand, player);
        Flash();
    }
}