using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
        public int threshold = 5;   // 每 threshold 张临时牌结算（由火花卡的 Count 变量设置，升级 5->4）。
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    // 每张火花生成独立实例：多个火花 buff 各自独立计数/阈值/倒计时，不合并堆叠（参照 TheBombPower）。
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/spark.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/spark.png");

    // 必须初始化 internal Data（参照 OrbitPower.InitInternalData）；否则 GetInternalData 返回共享/未初始化的 Data，threshold 异常。
    protected override object InitInternalData() => new Data();

    public void SetThreshold(int value)
    {
        var data = GetInternalData<Data>();
        data.threshold = Math.Max(1, value);
        ((IntVar)DynamicVars["Count"]).BaseValue = data.threshold;   // 供能力描述 {Count} 显示（升级后 4）
        InvokeDisplayAmountChanged();
    }

    // 倒计时显示：threshold → … → 1 → 结算后回到 threshold。
    public override int DisplayAmount => GetInternalData<Data>().tempPlayed % GetInternalData<Data>().threshold == 0
        ? GetInternalData<Data>().threshold
        : GetInternalData<Data>().threshold - (GetInternalData<Data>().tempPlayed % GetInternalData<Data>().threshold);

    // 能量/星辉图标、触发阈值 Count（能力描述用 {Energy:energyIcons()}{Stars:starIcons()}、{Count}）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Count", 5),
        new EnergyVar(1),
        new StarsVar(1),
    ];

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
        var threshold = Math.Max(1, data.threshold);
        data.tempPlayed++;
        InvokeDisplayAmountChanged();
        if (data.tempPlayed % threshold != 0)
            return;

        // 每累计 threshold 张：奖励 火花数量×1 能量与星辉（多个火花 buff 独立叠加）。
        await PlayerCmd.GainEnergy(Amount, player);
        await PlayerCmd.GainStars(Amount, player);
    }
}