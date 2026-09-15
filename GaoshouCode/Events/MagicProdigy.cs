// 魔法鬼才（Hive / ACT2）：从三张模组事件牌里学一个法术带走。
// 设计意图：ACT2 的「白嫖一张事件牌」——三张牌分别对应三种玩法方向
// （德罗普尼尔=经济/铺场、刮痧术=多段触发、神秘=抽牌与节奏），一次只能拿一张。
using System.Collections.Generic;
using System.Threading.Tasks;
using Gaoshou.Cards;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

[RegisterActEvent(typeof(Hive))]
public sealed class MagicProdigy : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png");

    // 只在 Hive（CurrentActIndex == 1）出现，且要过模组设置里的开关。
    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("magic_prodigy") && runState.CurrentActIndex == 1;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, LearnDraupnir, InitialOptionKey("DRAUPNIR"),
            HoverTipFactory.FromCardWithCardHoverTips<Draupnir>()),
        new EventOption(this, LearnPunchingTheAir, InitialOptionKey("PUNCHING"),
            HoverTipFactory.FromCardWithCardHoverTips<PunchingTheAir>()),
        new EventOption(this, LearnCryptic, InitialOptionKey("CRYPTIC"),
            HoverTipFactory.FromCardWithCardHoverTips<Cryptic>()),
    ];

    private Task LearnDraupnir() => LearnCard<Draupnir>();

    private Task LearnPunchingTheAir() => LearnCard<PunchingTheAir>();

    private Task LearnCryptic() => LearnCard<Cryptic>();

    /// <summary>
    /// 发一张模组卡并直接入牌组（拿卡必须先走 RunState.CreateCard，否则 CardPileCmd.Add 会抛）。
    /// </summary>
    private async Task LearnCard<T>() where T : CardModel
    {
        var card = Owner!.RunState.CreateCard<T>(Owner!);
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        await Cmd.CustomScaledWait(0.3f, 0.5f);

        SetEventFinished(PageDescription("DONE"));
    }
}
