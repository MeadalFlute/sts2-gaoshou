using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

// 神秘洞穴（出现章节：Overgrowth / ACT1）：
// 设计意图：越往里走奖励越好（金币 → 卡牌 → 遗物），但每深入一层都要付出生命。
// 玩家可以随时离开，属于「贪心换收益」的经典事件结构。
// 【2026-10-15 用户口径】每层消耗的生命改为**递增数列 3、5、7、9……**（每次 +2，起点 3）。
[RegisterActEvent(typeof(Overgrowth))]
public sealed class MysteriousCave : ModEventTemplate
{
    /// <summary>第一层要付的生命（3、5、7、9…… 的首项）。</summary>
    private const decimal CaveHpBase = 3m;

    /// <summary>每深入一层的生命增幅（+2）。</summary>
    private const decimal CaveHpStep = 2m;

    // 立绘：本模组自制底图（图由美术侧生成）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 洞里捡到的金币：基础 60，CalculateVars 里 ±10。
        new GoldVar("Gold1", 60),
        // 本层要付的生命：3 起步、每层 +2。变量同时驱动扣血与选项文案，
        // 每次付完立刻推进到下一层的数值（见 PayCaveHp）⇒ 文案写的就是下一步真扣的数 ✓
        new HpLossVar("CaveHp", CaveHpBase),
    ];

    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("mysterious_cave") && runState.CurrentActIndex == 0;

    public override void CalculateVars()
    {
        // 注意：变量名必须和 CanonicalVars 里声明的名字一致（这里是 "Gold1"）。
        // 曾经误写成 DynamicVars.Gold → 字典里没有 'Gold' 这个键，
        // BeginEvent 时抛 KeyNotFoundException（表现为进事件弹 "You found a bug"）。
        DynamicVars["Gold1"].BaseValue += Rng.NextInt(-10, 11);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, Dig, InitialOptionKey("DIG")),
        new EventOption(this, Leave, InitialOptionKey("LEAVE")),
    ];

    /// <summary>初入洞穴：拿到一笔金币，但被洞里的东西划伤（第一层：3 点生命）。</summary>
    private async Task Dig()
    {
        await PayCaveHp();
        await PlayerCmd.GainGold(DynamicVars["Gold1"].BaseValue, Owner!);
        // 洞穴有多张插图：逐场景换（原版 Trial 事件也是运行时换立绘）。
        EventArtOverlay.ShowInRoom("mysterious_cave_1");
        SetEventState(PageDescription("DEEP1"),
        [
            new EventOption(this, More, ModOptionKey("DEEP1", "MORE")),
            new EventOption(this, Leave2, ModOptionKey("DEEP1", "LEAVE2")),
        ]);
    }

    /// <summary>再探：一组普通卡牌奖励 + 继续掉血（第二层：5 点生命）。</summary>
    private async Task More()
    {
        await PayCaveHp();
        await RewardsCmd.OfferCustom(Owner!,
        [
            new CardReward(
                CardCreationOptions.ForNonCombatWithDefaultOdds([Owner!.Character.CardPool]),
                3, Owner),
        ]);
        EventArtOverlay.ShowInRoom("mysterious_cave_2");
        SetEventState(PageDescription("DEEP2"),
        [
            new EventOption(this, Bottom, ModOptionKey("DEEP2", "BOTTOM")),
            new EventOption(this, Leave3, ModOptionKey("DEEP2", "LEAVE3")),
        ]);
    }

    /// <summary>探到头：一个随机遗物 + 最后一次掉血（第三层：7 点生命）。</summary>
    private async Task Bottom()
    {
        await PayCaveHp();

        // 随机遗物可能被抽干（遗物池空了），必须判空。
        RelicModel? relic = RelicFactory.PullNextRelicFromFront(Owner!, RelicRarity.Uncommon, _ => true)?.ToMutable();
        if (relic != null)
            await RelicCmd.Obtain(relic, Owner!);

        EventArtOverlay.ShowInRoom("mysterious_cave_3");
        SetEventState(PageDescription("DEEP3"),
        [
            new EventOption(this, DoneOpt, ModOptionKey("DEEP3", "DONE_OPT"), true, true),
        ]);
    }

    /// <summary>
    /// 付出本层的生命，然后立刻把变量推进到**下一层**的数值（3 → 5 → 7 → 9……，每次 +2）。
    /// ⚠️ 必须在 <c>SetEventState</c> **之前**推进：选项文案里的 {CaveHp} 是渲染该页时取的，
    ///    先推进 ⇒ DEEP1/DEEP2 上写的就是下一层真正会扣的数（文案与结算不脱节）✓
    /// </summary>
    private async Task PayCaveHp()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature,
            DynamicVars["CaveHp"].BaseValue, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        DynamicVars["CaveHp"].BaseValue += CaveHpStep;
    }

    private async Task DoneOpt()
    {
        await Cmd.CustomScaledWait(0.3f, 0.5f);
        // 结束在 DONE 页（原来写的是不存在的 END 页 ⇒ 收尾文案会空掉）。
        // 单人自动离开、多人交给房间的「继续」按钮 —— 见 EventFlow：
        // ⚠️ 原来这里无条件调 NEventRoom.Proceed()，多人时会把**其他玩家**的客户端直接拽到地图（事件被跳过）。
        SetEventFinished(PageDescription("DONE"));
        await EventFlow.ProceedIfSinglePlayer(Owner);
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }

    private Task Leave2()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }

    private Task Leave3()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }
}
