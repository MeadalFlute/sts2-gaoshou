// 大书库（Hive + Glory / ACT2+ACT3）：借书（从全角色牌池里挑牌入组）或捐赠（删 1 张牌换 1 个随机遗物）。
// 设计意图：后期的「牌组整形站」——借书给的是广度（能拿到别的角色的牌），
// 捐赠给的是纯度（精简牌组）外加一份随机遗物，两者都只能做一次。
using MegaCrit.Sts2.Core.Nodes.Rooms;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

[RegisterActEvent(typeof(Hive))]
[RegisterActEvent(typeof(Glory))]
public sealed class GrandLibrary : ModEventTemplate
{
    // 网格里给几张候选、最多能拿几张。
    private const int OfferedCardCount = 20;
    private const int MaxSelectCount = 2;

    // 立绘名 = GaoshouEventSettings 里的事件键（小写下划线），与磁盘上的图片/`.import` 一致。
    // 注意：RitsuLib 不做 PascalCase↔snake_case 转换，写成 {GetType().Name} 会找不到图（静默回退、立绘空白）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png");

    // 只在 Hive(1) / Glory(2) 出现，且要过模组设置里的开关。
    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("grand_library") && runState.CurrentActIndex is 1 or 2;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, Borrow, InitialOptionKey("BORROW")),
        new EventOption(this, Donate, InitialOptionKey("DONATE")),
        new EventOption(this, Leave, InitialOptionKey("LEAVE")),
    ];

    /// <summary>借阅：从 20 张任意角色的牌里选至多 2 张加入牌组。</summary>
    private async Task Borrow()
    {
        // 多池：所有已解锁角色的卡池（和原版「色彩哲学家」一个路子）。
        var pools = Owner!.UnlockState.CharacterCardPools.ToList();

        // 均匀稀有度 + 只保留普通/罕见/稀有：角色池里还混着 Basic/Event/Token
        // （例如本模组自己的事件牌、衍生牌），不应该出现在书库的借阅清单里。
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds(pools,
                c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);

        var cards = CardFactory.CreateForReward(Owner!, OfferedCardCount, options).ToList();

        // 网格选牌界面的提示语必须用文案表里一定存在的键：NSimpleCardSelectScreen 会对
        // Prompt 调 GetFormattedText()，键缺失时抛 LocException 而不是只显示原始键名。
        // 规范 §1.6 写的 PageDescription("BORROW") 表里没有（BORROW 是选项名不是页名），
        // 所以这里退回用「借阅」选项自己的 .description。
        var prompt = GetOptionDescription(InitialOptionKey("BORROW")) ?? PageDescription("INITIAL");

        // base 方法会自动把选中的牌加进牌组；MinSelect=0 允许「一张都不拿」直接确认。
        await SelectCardsToAddToDeckFromGrid(cards, new CardSelectorPrefs(prompt, 0, MaxSelectCount));

        GoToAfterPage();
    }

    /// <summary>捐赠：移除 1 张牌，换 1 个随机遗物。</summary>
    private async Task Donate()
    {
        // 删牌：MaxSelect=MinSelect=1，所以只要能选就一定选中 1 张；牌组里没有可移除的牌时返回空集合。
        var removed = (await CardSelectCmd.FromDeckForRemoval(
            Owner!, new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1))).ToList();
        if (removed.Count > 0)
        {
            await CardPileCmd.RemoveFromDeck(removed);
        }

        // 随机遗物：按稀有度权重从本局抓包里抽；抓包里的模型是 canonical，
        // 必须先 ToMutable()，否则 RelicCmd.Obtain 里的 AssertMutable 会抛。
        var relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
        await RelicCmd.Obtain(relic, Owner!);

        GoToAfterPage();
    }

    /// <summary>离开（初始页）：直接结束事件。</summary>
    private Task Leave()
    {
        SetEventFinished(PageDescription("DONE"));
        return Task.CompletedTask;
    }

    /// <summary>离开（AFTER 页）：AFTER 的旁白本身就是结束句 → 直接结束（空文案 pages.END，避免重复）。</summary>
    private async Task LeaveAfter()
    {
        // AFTER 页本身就是结束页 → 结束并直接离开事件。
        SetEventFinished(PageDescription("END"));
        await Cmd.CustomScaledWait(0.35f, 0.5f);
        await NEventRoom.Proceed();
    }

    /// <summary>借书/捐赠都进同一页 AFTER，页面上只有一个「走了！」。</summary>
    private void GoToAfterPage()
    {
        // 每次都要 new 新的 EventOption：EventOption.Chosen() 有 WasChosen 守卫，复用实例点第二次不触发。
        SetEventState(PageDescription("AFTER"),
        [
            new EventOption(this, LeaveAfter, ModOptionKey("AFTER", "LEAVE2")),
        ]);
    }
}
