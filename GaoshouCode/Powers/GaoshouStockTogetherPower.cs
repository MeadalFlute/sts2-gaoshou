using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 囤积癖（能力）：以 buff 层数计数，层数 = 本场战斗全队打出「囤积癖」的累计次数。
// 该玩家的回合开始时，对所有敌人造成「该玩家当前囤积层数 × 层数」点伤害。
// 多人安全：能力挂在每个玩家身上，各自在自己回合开始结算（per-owner 守门）。
[RegisterPower]
public sealed class GaoshouStockTogetherPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 占位素材：暂用「囤积」的 buff 图标，正式图标确认后替换 stocktogether.png。
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/hoard.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/hoard.png");

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 只在拥有者自己的回合开始结算（多人：每个玩家各自结算自己的层数）。
        if (player != Owner.Player || Amount <= 0)
            return;

        // 囤积层数会被「保留」消耗，这里按当前实时层数计算。
        int hoard = Owner.GetPowerAmount<GaoshouHoardPower>();
        if (hoard <= 0)
            return;

        if (Owner.CombatState is not { } combatState)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies, hoard * Amount,
            ValueProp.Unpowered, Owner);
    }
}
