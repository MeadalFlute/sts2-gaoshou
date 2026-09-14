using MegaCrit.Sts2.Core.Entities.Powers;
    using System.Collections.Generic;
    using MegaCrit.Sts2.Core.Localization;
    using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
    using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 遵循古道（能力）：持有时，获得临时力量/临时敏捷改为消耗 1 辉星、获得 1 层基础力量/敏捷。
// 实际转换逻辑写在 GaoshouTemporaryStrengthPower / GaoshouTemporaryDexterityPower 的 GrantAsync 中。
//
// 描述变量踩坑：PowerModel.Description 拿到的 LocString **不带变量**，游戏只在 smartDescription 分支里
// 调 DynamicVars.AddTo(...)。所以只写 .description 又在文案里用 {StarCost}/{GainAmount} 时，占位符会原样显示。
// 两处预防：
//   1) localization 里同时提供 .description 与 .smartDescription（与模组其它 power 一致）；
//   2) 这里覆写 Description 手动注入自定义变量，非 smartDescription 的显示路径也能正确替换。
[RegisterPower]
public sealed class OldWayPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 消耗辉星数 / 改为获得的基础力量·敏捷层数（GrantAsync 读取，供描述引用）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("StarCost", 1),
        ModCardVars.Int("GainAmount", 1),
    ];

    public override LocString Description
    {
        get
        {
            var text = base.Description;
            DynamicVars.AddTo(text);
            return text;
        }
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/oldway.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/oldway.png");
}