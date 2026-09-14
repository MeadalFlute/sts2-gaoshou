using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 百货战神（能力）：每当你打出一张【临时】牌后，随机获得等同于本能力层数的临时力量或临时敏捷
// （层数由施加它的卡牌变量决定；回合结束减半的临时增益，直接与力量/敏捷同步授予）。
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

        // 随机：临时力量 或 临时敏捷（层数 = 本能力层数，即施加它的卡牌变量值）。
        if (player.RunState.Rng.Niche.NextBool())
            await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, player.Creature, Amount, player.Creature, cardPlay.Card);
        else
            await GaoshouTemporaryDexterityPower.GrantAsync(choiceContext, player.Creature, Amount, player.Creature, cardPlay.Card);
    }
}