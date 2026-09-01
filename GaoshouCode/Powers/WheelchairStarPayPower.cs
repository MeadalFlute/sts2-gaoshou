using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 轮椅·星辉代付（能力）：能量不足时，允许用星辉支付卡牌的能量。
// ShouldPayExcessEnergyCostWithStars 仅由战斗内模型（能力等）响应，遗物本体不在查询列，故经此能力实现。
[RegisterPower]
public sealed class WheelchairStarPayPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/deptgod.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/deptgod.png");

    public override bool ShouldPayExcessEnergyCostWithStars(Player player)
    {
        return player == Owner.Player;
    }
}