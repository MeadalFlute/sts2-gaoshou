using MegaCrit.Sts2.Core.Nodes.Rooms;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Gaoshou.Patches;
// 原版卡池里也有一张同名的 UltimateStrike，直接 using Gaoshou.Cards 会有歧义；
// 这里用别名明确指向本模组的「无敌打击」。
using ModUltimateStrike = Gaoshou.Cards.UltimateStrike;

namespace Gaoshou.Events;

// 打击大师（GAOSHOU_EVENT_SMITE_MASTER）：ACT1「繁茂之地 Overgrowth」+ ACT2「蜂巢 Hive」。
// 设计意图：把"打击"这个最基础的动作做出仪式感——要么学一手原版的完美打击（打击数量越多越强），
// 要么学本模组的无敌打击（按打击数量叠加攻击次数）；拒绝的玩家拿一组普通牌奖励走人。
// 选过任一种"学习"后置 GaoshouEventFlags.LearnedFromSmiteMaster，供「格挡达人」解锁隐藏第三选项。
[RegisterActEvent(typeof(Overgrowth))]
[RegisterActEvent(typeof(Hive))]
public sealed class SmiteMaster : ModEventTemplate
{
    // 设置页里的键（GaoshouEventSettings.ModEvents）。
    private const string SettingsKey = "smite_master";

    // 事件立绘（背景图位置）：走本模组自己的事件美术，约定文件名＝事件键（snake_case），
    // 图片由美术那一路生成，这里只声明路径。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/_base_notebook.png");

    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled(SettingsKey) && runState.CurrentActIndex is 0 or 1;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, LearnBasic, InitialOptionKey("LEARN_BASIC"),
            WithBasicCardTip(CardTag.Strike, HoverTipFactory.FromCardWithCardHoverTips<PerfectedStrike>())),
        new EventOption(this, LearnUltimate, InitialOptionKey("LEARN_ULTIMATE"),
            WithBasicCardTip(CardTag.Strike, HoverTipFactory.FromCardWithCardHoverTips<ModUltimateStrike>())),
        new EventOption(this, Refuse, InitialOptionKey("REFUSE")),
    ];

    /// <summary>
    /// 在给定释义前面补一条「本角色的初始打击/防御牌」的卡片释义。
    /// 选项文案里只写「打击」，具体是哪张牌交给 tooltip（原版也是"文字简洁 + 悬停看牌"的做法）。
    /// 角色卡池没有对应基础牌时不补这一条（返回原释义），不影响事件逻辑。
    /// </summary>
    private IEnumerable<IHoverTip> WithBasicCardTip(CardTag tag, IEnumerable<IHoverTip> tips)
    {
        var card = FindCharacterBasicCard(tag);
        return card == null ? tips : [HoverTipFactory.FromCard(card), .. tips];
    }

    // ---- 选项：学基础打击 ----

    private async Task LearnBasic()
    {
        // 标记"向大师学过打击"：格挡达人的隐藏第三选项靠它解锁。
        GaoshouEventFlags.LearnedFromSmiteMaster = true;
        await GrantStrikesAndCard<PerfectedStrike>(1);
        GoToTaught();
    }

    // ---- 选项：学终极奥义 ----

    private async Task LearnUltimate()
    {
        GaoshouEventFlags.LearnedFromSmiteMaster = true;
        await GrantStrikesAndCard<ModUltimateStrike>(2);
        GoToTaught();
    }

    // ---- 选项：拒绝 ----

    private async Task Refuse()
    {
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds([Owner!.Character.CardPool], c => c.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);

        // 三选一的普通牌奖励：奖励界面会等玩家领完，之后再收掉事件。
        await RewardsCmd.OfferCustom(Owner!, [new CardReward(options, 3, Owner!)]);
        SetEventFinished(PageDescription("DONE"));
    }

    // ---- 页面流转 ----

    // 学完后的 TAUGHT 页：只有一个"走了！"。
    private void GoToTaught()
    {
        SetEventState(PageDescription("TAUGHT"),
        [
            new EventOption(this, Leave, ModOptionKey("TAUGHT", "LEAVE")),
        ]);
    }

    private async Task Leave()
    {
        // TAUGHT 页本身就是结束页 → 结束并直接离开（不再多按一次"继续"）。
        SetEventFinished(PageDescription("END"));
        await Cmd.CustomScaledWait(0.35f, 0.5f);
        await NEventRoom.Proceed();
    }

    // ---- 给牌 ----

    /// <summary>
    /// 给玩家指定张数的本角色初始打击牌，外加一张 <typeparamref name="T" />，一起入牌组并统一预览。
    /// 每次入组都用 RunState.CreateCard 新出一张实例——同一个 mutable 卡对象不能重复进牌组。
    /// </summary>
    private async Task GrantStrikesAndCard<T>(int strikeCount) where T : CardModel
    {
        var player = Owner!;
        var strike = FindCharacterBasicCard(CardTag.Strike);

        // 2 张打击 + 1 张奖励牌一起预览，观感和原版"一次性给一叠牌"一致。
        var results = new List<CardPileAddResult>(strikeCount + 1);
        for (var i = 0; i < strikeCount; i++)
        {
            if (strike == null)
                break;

            results.Add(await CardPileCmd.Add(player.RunState.CreateCard(strike, player), PileType.Deck));
        }

        results.Add(await CardPileCmd.Add(player.RunState.CreateCard<T>(player), PileType.Deck));
        CardCmd.PreviewCardPileAdd(results, 1.2f, CardPreviewStyle.EventLayout);

        await Cmd.CustomScaledWait(0.3f, 0.5f);
    }

    /// <summary>
    /// 本角色开局牌组里的初始打击牌（canonical）；异常角色（初始牌组里没有打击）返回 null。
    /// 走 <see cref="CharacterBasicCards" />（CharacterModel.StartingDeck），不要用卡池 + Basic 稀有度去猜。
    /// </summary>
    private CardModel? FindCharacterBasicCard(CardTag tag)
    {
        var card = CharacterBasicCards.FindBasicOrNull(Owner, tag);

        if (card == null)
            Entry.Logger.Warn($"[SmiteMaster] character '{Owner?.Character.Id.Entry}' has no basic '{tag}' card; skipping.");

        return card;
    }
}
