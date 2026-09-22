// 万事屋（Overgrowth / ACT1）：花金币训练升级、打工赚金币、或者休息回血。
// 设计意图：ACT1 的三选一「资源转换站」——金币↔战力、时间↔金币、生命↔安全，
// 让玩家能把当前最缺的资源补上，但每种选择都要付出别的资源（或只是错过另外两种）。
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

[RegisterActEvent(typeof(Overgrowth))]
public sealed class OddJobs : ModEventTemplate
{
    // 基准值：文案里的 {TrainCost} / {WorkGold} 走 CalculateVars 掷 ±，{Heal} 固定 20。
    private const int TrainCostBase = 50;
    private const int WorkGoldBase = 100;
    private const int RestHealAmount = 20;

    // 训练要选的牌数（升级 2 张）——写成常量方便以后调。
    private const int TrainUpgradeCount = 2;

    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar("TrainCost", TrainCostBase),
        new GoldVar("WorkGold", WorkGoldBase),
        new HealVar("Heal", RestHealAmount),
    ];

    // 只在 Overgrowth 出现，且要过模组设置里的开关。
    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("odd_jobs") && runState.CurrentActIndex == 0;

    // 进事件时掷随机：训练费 50±10，打工钱 100±20（上界不含）。
    public override void CalculateVars()
    {
        DynamicVars["TrainCost"].BaseValue += Rng.NextInt(-10, 11);
        DynamicVars["WorkGold"].BaseValue += Rng.NextInt(-20, 21);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        // 金币不足时「训练」直接**锁定**（EventOption 的 IsLocked 由 onChosen == null 决定，
        // 原版 LuminousChoir / TeaMaster、本模组神秘商店 / 超级升级器都是这套做法）：
        // 显示灰色不可点，而不是"能点、点了把身上的钱掏空还照样升级"。
        var canPay = (Owner?.Gold ?? 0m) >= DynamicVars["TrainCost"].BaseValue;
        Func<Task>? train = canPay ? Train : null;

        return
        [
            new EventOption(this, train, InitialOptionKey(canPay ? "TRAIN" : "TRAIN_LOCKED")),
            new EventOption(this, Work, InitialOptionKey("WORK")),
            new EventOption(this, RestUp, InitialOptionKey("REST")),
        ];
    }

    /// <summary>训练：失去金币，选 2 张牌升级。</summary>
    private async Task Train()
    {
        // 金币不足时该选项已被锁定（见 GenerateInitialOptions），不会走到这里；
        // 另外 PlayerCmd.LoseGold 内部会把金币夹到 0 以上，即便被外部强制调用也不会变成负数。
        await PlayerCmd.LoseGold(DynamicVars["TrainCost"].BaseValue, Owner!, GoldLossType.Spent);

        // FromDeckForUpgrade 自带「可升级」过滤；可升级牌不足 2 张时会直接返回全部，不会卡住。
        var picked = (await CardSelectCmd.FromDeckForUpgrade(
            Owner!, new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, TrainUpgradeCount))).ToList();

        foreach (var card in picked)
        {
            CardCmd.Upgrade(card);   // 非 async，逐张升级并等一小段时间让预览动画播完
            await Cmd.CustomScaledWait(0.3f, 0.5f);
        }

        SetEventFinished(PageDescription("DONE"));
    }

    /// <summary>打工：获得金币。</summary>
    private async Task Work()
    {
        await PlayerCmd.GainGold(DynamicVars["WorkGold"].BaseValue, Owner!);
        SetEventFinished(PageDescription("DONE"));
    }

    /// <summary>休息：回血（CreatureCmd.Heal 会自己夹到上限）。</summary>
    private async Task RestUp()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars["Heal"].BaseValue);
        SetEventFinished(PageDescription("DONE"));
    }
}
