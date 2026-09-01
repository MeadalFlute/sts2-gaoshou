using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 遵循古道（能力）：持有时，获得临时力量/临时敏捷改为消耗 1 星辉、获得 1 层基础力量/敏捷。
// 实际转换逻辑写在 GaohouTemporaryStrengthPower / GaohouTemporaryDexterityPower 的 GrantAsync 中。
[RegisterPower]
public sealed class OldWayPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/oldway.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/oldway.png");
}