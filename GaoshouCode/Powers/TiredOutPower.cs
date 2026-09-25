using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 累坏了（能力）：**没有任何效果**，纯粹是给玩家的一个提示 —— 你这回合已经打出过带「限制」的牌，
/// 所以本回合不能再打第二张。
// 由 LimitedPlayRule 在打出限制牌时挂上、并在你的下个回合开始时移除（与"限制"额度的重置同一时机）。
// 用 PowerStackType.Single：图标上不显示层数（它是一句话，不是计数）。
[RegisterPower]
public sealed class TiredOutPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/tiredout.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/tiredout.png");
}
