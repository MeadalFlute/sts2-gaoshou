using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 百货战神（能力）：每当你打出一张【临时】牌后，获得等同于本能力层数的临时力量"和"临时敏捷
// （层数由施加它的卡牌变量决定；回合结束减半的临时增益，直接与力量/敏捷同步授予）。
// 1.1.13 平衡调整：由"随机获得其中之一"改为"同时获得两者"（顺带不再消耗同步随机数）。
[RegisterPower]
public sealed class DepartmentStoreGodPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/deptgod.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/deptgod.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        if (Owner.Player == null || player.NetId != Owner.Player.NetId)
            return;
           
        if (cardPlay.Card.DeckVersion != null)
            return;

        // 临时力量与临时敏捷同时获得（层数 = 本能力层数，即施加它的卡牌变量值）。
        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, player.Creature, Amount, player.Creature, cardPlay.Card);
        await GaoshouTemporaryDexterityPower.GrantAsync(choiceContext, player.Creature, Amount, player.Creature, cardPlay.Card);
    }
}