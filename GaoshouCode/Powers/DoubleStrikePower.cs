using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 连击（能力）：你的所有"打击"牌（牌名含 STRIKE）伤害 +Amount。
// 图标：CardArtAnger（自定义 buff 用）。
[RegisterPower]
public sealed class DoubleStrikePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/doublestrike.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/doublestrike.png");

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 仅加成"打击"牌：牌名（Id）包含 STRIKE（连环打击 LINKED_STRIKE 等亦命中）。
        if (Owner != dealer || cardSource == null)
            return 0m;
        if (!cardSource.Id.Entry.Contains("STRIKE"))
            return 0m;
        return Amount;
    }
}