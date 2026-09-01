using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 武学宗师（能力）：每当你触发【流转】后，获得 Amount 层「临时力量」。
// 流转仍为简化版（颜色判定未实装）：只要打出的牌带「流转」词条即视为触发。
// 临时力量必须与力量同步授予（走 GaoshouTemporaryStrengthPower.GrantAsync）。
[RegisterPower]
public sealed class GrandMasterPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/grandmaster.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/grandmaster.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        // 仅当打出的牌「流转」判定成功（颜色与上一张牌完全不同）时触发；只响应装备者自己的打出（多人防串触发）。
        if (player == null || Amount <= 0 || player != Owner.Player || !GaoshouFlowTracker.IsFlowReady(cardPlay.Card))
            return;

        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, player.Creature, Amount, player.Creature, cardPlay.Card);
    }
}