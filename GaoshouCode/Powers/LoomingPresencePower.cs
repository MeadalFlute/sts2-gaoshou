using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
    using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
    using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 浮光掠影（能力）：每当你通过打出的卡牌对敌人造成伤害时，获得与当前层数等量的格挡。
// 每当你受到未被格挡的伤害时，层数 -1（归零移除）。
[RegisterPower]
public sealed class LoomingPresencePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 每次受到未被格挡的伤害时减少的层数。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("LossPerHit", 1),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/loomingpresence.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/loomingpresence.png");

    // 仅响应有卡牌来源的伤害；能力（例如势不可当）造成的伤害没有 cardSource。
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature? target, CardModel? cardSource)
    {
        if (dealer != Owner || cardSource == null || Amount <= 0)
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
        var loss = DynamicVars.GetRequired<IntVar>("LossPerHit").IntValue;
        if (Amount <= loss)
        {
            await PowerCmd.Remove(this);
            return;
        }
        Amount -= loss;
        InvokeDisplayAmountChanged();
    }
}
