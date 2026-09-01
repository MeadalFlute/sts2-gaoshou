using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 浮光掠影（能力）：每当你对敌人造成伤害时，获得与当前层数等量的格挡。
// 每当你受到未被格挡的伤害时，层数 -1（归零移除）。
[RegisterPower]
public sealed class LoomingPresencePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/loomingpresence.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/loomingpresence.png");

    // 每当你对敌人造成伤害时：获得与当前层数等量的格挡。
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature? target, CardModel? cardSource)
    {
        if (dealer != Owner || Amount <= 0)
            return;
        // 不吃敏捷（Unpowered）。
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move | ValueProp.Unpowered, null);
    }

    // 每当你受到未被格挡的伤害时：层数 -1（归零移除）。
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature? target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0)
            return;
        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }
        Amount -= 1;
        InvokeDisplayAmountChanged();
    }
}