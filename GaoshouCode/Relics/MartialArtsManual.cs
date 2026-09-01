using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 武林秘籍（稀有）：每当你消耗能量时，获得 1 层临时敏捷；每当你消耗星辉时，获得 1 层临时力量。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class MartialArtsManual : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

        protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Energy("EnergyB", 1),
        ModCardVars.Stars("Star", 1),
        ModCardVars.Stars("StarB", 1),
    ];


public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 消耗能量 → 临时敏捷。
    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (amount <= 0 || card.Owner != Owner)
            return;
        var n = amount >= 2 ? amount / 2 : 0;
        if (n > 0)
            await GaoshouTemporaryDexterityPower.GrantAsync(new ThrowingPlayerChoiceContext(), Owner.Creature, n, Owner.Creature, card);
    }

    // 消耗星辉 → 临时力量。
    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (amount <= 0 || spender != Owner)
            return;
        await GaoshouTemporaryStrengthPower.GrantAsync(new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null);
    }
}