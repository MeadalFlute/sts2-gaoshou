using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 至多保留（能力）：你的回合开始时，最多保留与层数等量的格挡（超出部分失去）。
[RegisterPower]
public sealed class GaoshouRetainBlockPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/fullyarmed.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/fullyarmed.png");

    // 持有者不清除格挡。
    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    // 清除被阻止后：若格挡超出上限，截断到上限。
    // 但若场上还有其它"全保留格挡"的能力（残影/壁垒等，ShouldClearBlock=false），则让全保留优先，
    // 不再截断——否则会错误地把本该全保留的格挡砍到上限（例如残影+全副武装时格挡被重置到 15）。
    public override Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        if (this != preventer || creature != Owner)
            return Task.CompletedTask;

        foreach (var power in Owner.Powers)
        {
            if (power != this && !power.ShouldClearBlock(Owner))
                return Task.CompletedTask;
        }

        if (Owner.Block > Amount)
            Owner.Block = Amount;
        return Task.CompletedTask;
    }
}