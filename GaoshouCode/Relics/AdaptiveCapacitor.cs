using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 自适应电容（普通）：回合结束时，若你剩余能量大于能量上限，能量上限 +1。
// 实现：玩家回合开始时检查（上一回合结束时的剩余能量 == 当前开局能量）。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class AdaptiveCapacitor : ModRelicTemplate
{
    private int _bonusMaxEnergy;

    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || !participants.Contains(Owner.Creature))
            return;
        var pcs = Owner.PlayerCombatState;
        if (pcs.Energy > pcs.MaxEnergy)
        {
            _bonusMaxEnergy++;
            Flash();
        }
        await Task.CompletedTask;
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner)
            return amount;
        return amount + _bonusMaxEnergy;
    }
}