using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 剑舞（能力）：每当你触发【流转】效果时，抽 1 张牌，获得 1 能量。
// 触发判定：流转 = GaoshouFlowTracker.IsFlowReady(该卡)。
[RegisterPower]
public sealed class SwordDancePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/sworddance.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/sworddance.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        // 只响应装备者自己的打出（多人防串触发——否则主机打流转牌时客机的剑舞也会抽牌）。
        if (player == null || Amount <= 0 || player != Owner.Player)
            return;

        var card = cardPlay.Card;
        if (!card.Keywords.Contains(GaoshouKeyword.Flow) || !GaoshouFlowTracker.IsFlowReady(card))
            return;

        await CardPileCmd.Draw(choiceContext, 1, player);
        await PlayerCmd.GainEnergy(1, player);
    }
}