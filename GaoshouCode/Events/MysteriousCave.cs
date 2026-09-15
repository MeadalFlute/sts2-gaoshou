using MegaCrit.Sts2.Core.Nodes.Rooms;
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
// 设计意图：越往里走奖励越好（金币 → 卡牌 → 遗物），但每深入一层都要付出固定生命。
// 玩家可以随时离开，属于「贪心换收益」的经典事件结构。
[RegisterActEvent(typeof(Overgrowth))]
public sealed class MysteriousCave : ModEventTemplate
{
    // 立绘：本模组自制底图（图由美术侧生成）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 洞里捡到的金币：基础 60，CalculateVars 里 ±10。
        new GoldVar("Gold1", 60),
        // 每深入一层失去的生命：固定 6（无浮动，所以直接当常量用，不改 BaseValue）。
        new HpLossVar("CaveHp", 6m),
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

    /// <summary>初入洞穴：拿到一笔金币，但被洞里的东西划伤。</summary>
    private async Task Dig()
    {
        await LoseCaveHp();
        await PlayerCmd.GainGold(DynamicVars["Gold1"].BaseValue, Owner!);
        // 洞穴有多张插图：逐场景换（原版 Trial 事件也是运行时换立绘）。
        EventArtOverlay.ShowInRoom("mysterious_cave_1");
        SetEventState(PageDescription("DEEP1"),
        [
            new EventOption(this, More, ModOptionKey("DEEP1", "MORE")),
            new EventOption(this, Leave2, ModOptionKey("DEEP1", "LEAVE2")),
        ]);
    }

    /// <summary>再探：一组普通卡牌奖励 + 继续掉血。</summary>
    private async Task More()
    {
        await LoseCaveHp();
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

    /// <summary>探到头：一个随机遗物 + 最后一次掉血。</summary>
    private async Task Bottom()
    {
        await LoseCaveHp();

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

    private Task LoseCaveHp()
        => CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature,
            DynamicVars["CaveHp"].BaseValue, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);

    private async Task DoneOpt()
    {
        await Cmd.CustomScaledWait(0.3f, 0.5f);
        // DEEP3 页本身就是结束页：保留这段旁白、直接离开事件（连"继续"都不用再点一次）。
        SetEventFinished(PageDescription("END"));
        await Cmd.CustomScaledWait(0.35f, 0.5f);
        await NEventRoom.Proceed();
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
