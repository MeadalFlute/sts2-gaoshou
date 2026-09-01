using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 火花（能力）：每当你打出【临时】牌时计数，每累计 5 张获得 1 点能量与 1 点星辉。
// 计数直接使用 buff 的 Amount（同步且可见，参考储君-环绕轨道），无需私有字段。
// 临时机制：打出的牌「临时」（局内生成、不属于牌组 DeckVersion==null）。仅响应自己的打出（多人防串触发）。
[RegisterPower]
public sealed class SparkPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/spark.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/spark.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        // 只响应装备者自己的打出（多人防串触发）。
        if (player == null || player != Owner.Player)
            return;
        // 临时牌 = 局内生成、不属于牌组（DeckVersion == null）。
        if (cardPlay.Card.DeckVersion != null)
            return;

        // 计数 +1（Amount 为计数器，随战斗状态同步，客户端可见层数）。
        Amount++;
        InvokeDisplayAmountChanged();
        if (Amount < 5)
            return;

        Amount = 0;
        InvokeDisplayAmountChanged();
        await PlayerCmd.GainEnergy(1, player);
        await PlayerCmd.GainStars(1, player);
    }
}