using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 火花（能力，环绕轨道同款）：每当你打出【临时】牌时计数（倒计时显示）。
// 多个火花 buff 各自独立：Amount = 火花数量；每累计 5 张临时牌 → 奖励 数量×1 能量与星辉（数量叠加）。
// 临时机制：打出的牌「临时」（局内生成、不属于牌组 DeckVersion==null）。仅响应自己的打出（多人防串触发）。
[RegisterPower]
public sealed class SparkPower : ModPowerTemplate
{
    public class Data
    {
        public int tempPlayed;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/spark.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/spark.png");

    // 倒计时显示：5 → 4 → … → 1 → 结算后回到 5（环绕轨道同款 4 - n%4）。
    public override int DisplayAmount => 5 - GetInternalData<Data>().tempPlayed % 5;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        // 只响应装备者自己的打出（多人防串触发）。
        if (player == null || player != Owner.Player)
            return;
        // 临时牌 = 局内生成、不属于牌组（DeckVersion == null）。
        if (cardPlay.Card.DeckVersion != null)
            return;

        var data = GetInternalData<Data>();
        data.tempPlayed++;
        InvokeDisplayAmountChanged();
        if (data.tempPlayed % 5 != 0)
            return;

        // 每累计 5 张：奖励 火花数量×1 能量与星辉（多个火花 buff 独立叠加）。
        await PlayerCmd.GainEnergy(Amount, player);
        await PlayerCmd.GainStars(Amount, player);
    }
}