using System.Collections.Generic;
using System.Threading.Tasks;
using Gaoshou.Cards;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

// 奇怪神殿（出现章节：Overgrowth / ACT1）：
// 设计意图：一个迷之祭坛，直接拿钱（有代价，掉血）或者拿走神像（拿到本模组的事件牌「诅咒神像」）。
// 三个选项都是"立刻有结果"，所以一趟就是 DONE。
[RegisterActEvent(typeof(Overgrowth))]
public sealed class StrangeTemple : ModEventTemplate
{
    // 立绘：本模组自制底图（图由美术侧生成）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/strange_temple.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 神殿里的金币：基础 120，CalculateVars 里 ±10。
        new GoldVar("Gold", 120),
        // 拿走金币时震落的碎石造成的伤害：固定 6。
        new HpLossVar("TempleHp", 6m),
    ];

    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("strange_temple") && runState.CurrentActIndex == 0;

    public override void CalculateVars()
    {
        DynamicVars.Gold.BaseValue += Rng.NextInt(-10, 11);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, TakeGold, InitialOptionKey("TAKE_GOLD"))
            .ThatDoesDamage(DynamicVars["TempleHp"].BaseValue),
        new EventOption(this, TakeIdol, InitialOptionKey("TAKE_IDOL")),
        new EventOption(this, Leave, InitialOptionKey("LEAVE")),
    ];

    /// <summary>拿走金币：先挨一下（碎石/机关），再拿钱。</summary>
    private async Task TakeGold()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature,
            DynamicVars["TempleHp"].BaseValue, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner!);
        SetEventFinished(PageDescription("DONE"));
    }

    /// <summary>拿走神像：获得本模组的事件牌「诅咒神像」（必须入牌组）。</summary>
    private async Task TakeIdol()
    {
        CardModel card = Owner!.RunState.CreateCard<CursedIdol>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        SetEventFinished(PageDescription("DONE"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }
}
