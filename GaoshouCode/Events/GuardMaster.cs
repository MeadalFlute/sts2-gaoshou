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
// 设计意图：与「打击大师」互为镜像。三个常规选项是"练一练""教教我""打击全扔了"，
// 第四个选项是隐藏分支：只有本局在「打击大师」里真的向大师学过打击
// （GaoshouEventFlags.LearnedFromSmiteMaster）才会出现，作为"打击与格挡本是一家"的彩蛋，
// 奖励本模组的「打击格挡奥义」。标记随本局生命周期重置，见 GaoshouEventFlags。
//
// ⚠️ 收尾一律走 EventFlow.ProceedIfSinglePlayer（单人自动离开 / 多人交给「继续」按钮）：
//    2026-10-06 玩家反馈"多人里 1 个人选了就把事件跳过"，根因就是这里原来无条件调 NEventRoom.Proceed()。
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
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Times", TrainUpgradeCount),
        // 「打击全扔了」实际换掉的张数（页面文案 {Swapped}）——处理函数里按实际数量回填。
        new IntVar("Swapped", 0),
    ];

    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled(SettingsKey) && runState.CurrentActIndex is 1 or 2;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>(4)
        {
            // 「一起练练」只升级打击/防御（2026-10-12 用户口径：**不再**发一张防御 ✗）→ 仍给打击/防御两条 tooltip。
            new(this, Train, InitialOptionKey("TRAIN"),
                WithBasicCardTips([], CardTag.Strike, CardTag.Defend)),
            new(this, AskTeach, InitialOptionKey("ASK_TEACH"),
                WithBasicCardTips([HoverTipFactory.FromCard<Fasten>()], CardTag.Defend)),
        };

        // 「你说得对」（跳过训练）：**总是给这条**——牌组里没有可移除的基础打击时它就是纯跳过
        //（用户 2026-10-07 口径：允许空效果，走 DROPPED_EMPTY 收尾页）。
        options.Add(new(this, DropStrikes, InitialOptionKey("DROP_STRIKES"),
            WithBasicCardTips([], CardTag.Strike, CardTag.Defend)));

        // 隐藏分支：没跟打击大师学过打击就不把这条选项放进列表（而不是放进去再禁用）。
        if (GaoshouEventFlags.LearnedFromSmiteMaster)
            options.Add(new EventOption(this, MentionSmite, InitialOptionKey("MENTION_SMITE")));

        return options;
    }

    // ---- 选项：一起练练 ----

    /// <summary>
    /// 「那来一起练练！」：随机升级 <see cref="TrainUpgradeCount" /> 张可升级的初始打击/防御牌。
    /// **不发牌**（2026-10-12 用户口径：原来练完还会送一张防御，已去掉 ✗）—— 只升级，收益回到"训练"本身。
    /// </summary>
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

        GoToTrained();
    }

    // ---- 选项：请教 ----

    private async Task AskTeach()
    {
        await GrantCharacterBasicCard(CardTag.Defend);
        await GrantCard<Fasten>();
        GoToTrained();
    }

    // ---- 选项：你说得对（跳过训练）----

    /// <summary>
    /// "可移除的基础打击"：判据照抄原版 <c>Amalgamator.IsValid</c> —— **基础稀有度** + 带
    /// <see cref="CardTag.Strike" /> 标签 + 可移除。
    /// 只认基础牌是**用户 2026-10-07 明确选定的口径**：带「打击」标签的牌里混着大量稀有/强力攻击牌
    /// （本模组「无敌打击」、原版 灰烬打击/日耀打击/双生打击/完美打击/陨石打击…），
    /// 一次全删会变成"坑玩家的陷阱选项" ✗；原版对"牌组里的打击"也都是这个口径
    /// （<c>Amalgamator.IsValid</c> / <c>Tezcatara</c> / <c>GhostSeed</c> / <c>NutritiousSoup</c> 全部带 <c>Rarity == Basic</c>）。
    /// 选项文案也据此写成"所有**基础**打击牌"（原版涡旋事件同样用「基础[打击]」的说法）✓
    /// （升级过的 连环打击+ 仍是 Basic 稀有度，所以照样会被换掉。）
    /// </summary>
    private static bool IsRemovableBasicStrike(CardModel card)
        => card != null
           && card.Rarity == CardRarity.Basic
           && card.Tags.Contains(CardTag.Strike)
           && card.IsRemovable;

    /// <summary>
    /// 跳过训练（选项标题「你说得对」）：移除牌组里全部基础打击，并给予**等量的本角色防御牌**
    /// （角色不同就是不同的牌：高手是「连环防御」，铁甲战士是「防御」…走 CharacterBasicCards，不写死）。
    /// 选项的悬浮联想同样走 <c>CharacterBasicCards</c>（见 <see cref="WithBasicCardTips" />），
    /// 所以悬停看到的就是本角色的 打击 / 防御 两张牌 ✓
    ///
    /// 牌组里本来就没有基础打击时**也允许**点（用户 2026-10-07 口径：那就是纯跳过）：
    /// 不删不送，收尾走 <c>DROPPED_EMPTY</c> 页（避免"教了我 0 张防御"这种怪句子）✓
    /// </summary>
    private async Task DropStrikes()
    {
        var player = Owner!;
        var strikes = player.Deck.Cards.Where(IsRemovableBasicStrike).ToList();
        var defend = FindCharacterBasicCard(CardTag.Defend);

        // 张数先记下来（移除之后就数不到了），并回填给页面文案的 {Swapped}。
        var swapped = strikes.Count;
        DynamicVars["Swapped"].BaseValue = swapped;

        if (swapped > 0)
            await CardPileCmd.RemoveFromDeck(strikes);

        if (defend == null)
        {
            Entry.Logger.Warn($"[GuardMaster] character '{player.Character.Id.Entry}' has no basic '{CardTag.Defend}' card; nothing granted.");
        }
        else if (swapped > 0)
        {
            // 一次性把等量的防御牌塞进牌组，再统一预览（观感同原版"给一叠牌"）。
            var results = new List<CardPileAddResult>(swapped);
            for (var i = 0; i < swapped; i++)
                results.Add(await CardPileCmd.Add(player.RunState.CreateCard(defend, player), PileType.Deck));

            CardCmd.PreviewCardPileAdd(results, 1.2f, CardPreviewStyle.EventLayout);
            await Cmd.CustomScaledWait(0.3f, 0.5f);
        }

        // 换到了牌 → DROPPED（带 {Swapped} 张数）；一张没换 → DROPPED_EMPTY（纯跳过的旁白）。
        SetEventFinished(PageDescription(swapped > 0 ? "DROPPED" : "DROPPED_EMPTY"));
        await EventFlow.ProceedIfSinglePlayer(Owner);
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
        // 结束在专属收尾页 MASTERED（原来写的是不存在的 END 页 ⇒ 收尾文案会空掉）。
        SetEventFinished(PageDescription("MASTERED"));
        await EventFlow.ProceedIfSinglePlayer(Owner);
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

    private async Task Leave()
    {
        // 结束在 DONE 页（原来写的是不存在的 END 页 ⇒ 收尾文案会空掉）。
        SetEventFinished(PageDescription("DONE"));
        await EventFlow.ProceedIfSinglePlayer(Owner);
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
