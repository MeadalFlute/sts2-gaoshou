using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Gaoshou.Cards;
using Gaoshou.Patches;

namespace Gaoshou.Events;

// 格挡达人（GAOSHOU_EVENT_GUARD_MASTER）：ACT2「蜂巢 Hive」+ ACT3「荣光 Glory」。
// 设计意图：与「打击大师」互为镜像。前两个选项是"练一练"和"教教我"，
// 第三个选项是隐藏分支：只有本局在「打击大师」里真的向大师学过打击
// （GaoshouEventFlags.LearnedFromSmiteMaster）才会出现，作为"打击与格挡本是一家"的彩蛋，
// 奖励本模组的「打击格挡奥义」。标记随本局生命周期重置，见 GaoshouEventFlags。
[RegisterActEvent(typeof(Hive))]
[RegisterActEvent(typeof(Glory))]
public sealed class GuardMaster : ModEventTemplate
{
    // 设置页里的键（GaoshouEventSettings.ModEvents）。
    private const string SettingsKey = "guard_master";

    // 「那来一起练练！」随机升级的基础牌张数（只在代码里用，同时作为文案占位符 {Times}）。
    private const int TrainUpgradeCount = 2;

    // 事件立绘（背景图位置）：走本模组自己的事件美术，约定文件名＝事件键（snake_case），
    // 图片由美术那一路生成，这里只声明路径。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/guard_master.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Times", TrainUpgradeCount),
    ];

    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled(SettingsKey) && runState.CurrentActIndex is 1 or 2;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>(3)
        {
            // 「一起练练」会升级打击/防御、还会发一张防御 → 两条 tooltip 都给上。
            new(this, Train, InitialOptionKey("TRAIN"),
                WithBasicCardTips([], CardTag.Strike, CardTag.Defend)),
            new(this, AskTeach, InitialOptionKey("ASK_TEACH"),
                WithBasicCardTips([HoverTipFactory.FromCard<Fasten>()], CardTag.Defend)),
        };

        // 隐藏分支：没跟打击大师学过打击就不把这条选项放进列表（而不是放进去再禁用）。
        if (GaoshouEventFlags.LearnedFromSmiteMaster)
            options.Add(new EventOption(this, MentionSmite, InitialOptionKey("MENTION_SMITE")));

        return options;
    }

    // ---- 选项：一起练练 ----

    private async Task Train()
    {
        var times = DynamicVars["Times"].IntValue;

        // 候选：牌组里"初始打击/防御"且还能升级的牌。选中后立刻从候选里剔除，
        // 保证同一次训练不会把同一张牌升级两遍。
        var candidates = Owner!.Deck.Cards
            .Where(c => c != null && c.IsBasicStrikeOrDefend && c.IsUpgradable)
            .ToList();

        for (var i = 0; i < times && candidates.Count > 0; i++)
        {
            var index = Rng.NextInt(candidates.Count);
            var card = candidates[index];
            candidates.RemoveAt(index);

            CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
            await Cmd.CustomScaledWait(0.3f, 0.5f);
        }

        // 练完再送一张初始防御牌。
        await GrantCharacterBasicCard(CardTag.Defend);
        GoToTrained();
    }

    // ---- 选项：请教 ----

    private async Task AskTeach()
    {
        await GrantCharacterBasicCard(CardTag.Defend);
        await GrantCard<Fasten>();
        GoToTrained();
    }

    // ---- 选项：提起打击大师（隐藏分支）----

    private Task MentionSmite()
    {
        // 注意：本页每次都要 new 新的 EventOption，否则第二次点击不会再触发回调。
        SetEventState(PageDescription("ENLIGHTENED"),
        [
            new EventOption(this, Understood, ModOptionKey("ENLIGHTENED", "UNDERSTOOD"),
                HoverTipFactory.FromCardWithCardHoverTips<StrikeDefendMastery>()),
        ]);
        return Task.CompletedTask;
    }

    private async Task Understood()
    {
        await GrantCard<StrikeDefendMastery>();
        // ENLIGHTENED 页的旁白已经收尾（"…看来打击格挡都十分重要，我悟了！"）→ 直接结束。
        SetEventFinished(PageDescription("ENLIGHTENED"));
    }

    // ---- 页面流转 ----

    // 前两条路径共用 TRAINED 页：只有一个"走了！"。
    private void GoToTrained()
    {
        SetEventState(PageDescription("TRAINED"),
        [
            new EventOption(this, Leave, ModOptionKey("TRAINED", "LEAVE")),
        ]);
    }

    private Task Leave()
    {
        // TRAINED 页的旁白已经收尾（"大哥拍拍我的肩膀，让我明天接着来。"）→ 直接结束。
        SetEventFinished(PageDescription("TRAINED"));
        return Task.CompletedTask;
    }

    // ---- 给牌 ----

    /// <summary>
    /// 把「本角色的初始打击/防御牌」的卡片释义补到选项的悬浮提示最前面。
    /// 选项文案里只写「打击」「防御」，具体是哪张牌交给 tooltip（原版也是"文字简洁 + 悬停看牌"的做法）。
    /// 角色卡池没有对应基础牌时跳过那一条，不影响事件逻辑。
    /// </summary>
    private IEnumerable<IHoverTip> WithBasicCardTips(IEnumerable<IHoverTip> tips, params CardTag[] tags)
    {
        var extra = tags
            .Select(FindCharacterBasicCard)
            .Where(c => c != null)
            .Select(c => HoverTipFactory.FromCard(c!));

        return [.. extra, .. tips];
    }

    /// <summary>
    /// 本角色开局牌组里的初始打击/防御牌（canonical）；异常角色返回 null。
    /// 走 <see cref="CharacterBasicCards" />（CharacterModel.StartingDeck）——用 <c>CardPool.AllCards</c> + Basic 稀有度
    /// 会命中原版共享池里的铁甲战士打击/防御（玩家实测：选项 2 的 tooltip 联想到了铁甲战士的基础打防）。
    /// </summary>
    private CardModel? FindCharacterBasicCard(CardTag tag)
        => CharacterBasicCards.FindBasicOrNull(Owner, tag);

    /// <summary>把本角色的一张初始防御牌加入牌组并预览。</summary>
    private async Task GrantCharacterBasicCard(CardTag tag)
    {
        var canonical = FindCharacterBasicCard(tag);

        if (canonical == null)
        {
            Entry.Logger.Warn($"[GuardMaster] character '{Owner?.Character.Id.Entry}' has no basic '{tag}' card; skipping.");
            return;
        }

        await GrantCard(canonical);
    }

    private Task GrantCard<T>() where T : CardModel => GrantCard(ModelDb.Card<T>());

    /// <summary>把一张（canonical）牌的新实例加入牌组并预览。</summary>
    private async Task GrantCard(CardModel canonical)
    {
        var player = Owner!;
        var card = player.RunState.CreateCard(canonical, player);

        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        await Cmd.CustomScaledWait(0.3f, 0.5f);
    }
}
