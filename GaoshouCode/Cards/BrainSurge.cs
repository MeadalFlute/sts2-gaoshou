using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 脑力激荡（普通）：1 能量 0 星辉。
// 丢弃 1 张牌，从弃牌堆选择 2 张牌加入手牌。
// 词条：幻影、限制、消耗 —— 由 CanonicalKeywords 声明，游戏自动追加词条行（文案里不重复写）。
// 升级：费用 1 -> 0。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class BrainSurge : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Phantom,
        GaoshouKeyword.Limited,
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("DiscardCount", 1),   // 先弃几张
        ModCardVars.Int("PickCount", 2),      // 再从弃牌堆取几张
    ];

    public BrainSurge() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 1) 先丢弃 1 张手牌。走 CardCmd.Discard（而非直接搬牌堆），这样"被丢弃时"的效果（Sly 等）照常触发。
        var discardCount = DynamicVars.GetRequired<IntVar>("DiscardCount").IntValue;
        if (discardCount > 0 && PileType.Hand.GetPile(player).Cards.Count != 0)
        {
            var discardPrefs = new CardSelectorPrefs(
                new LocString("cards", "GAOSHOU_BRAIN_SURGE_DISCARD_PROMPT"), discardCount, discardCount);
            var toDiscard = (await CardSelectCmd.FromHand(choiceContext, player, discardPrefs, null, this)).ToList();
            if (toDiscard.Count != 0)
                await CardCmd.Discard(choiceContext, toDiscard);
        }

        // 2) 从弃牌堆选择**最多** 2 张牌加入手牌（刚弃掉的那张此时也在弃牌堆里，可以被选回）。
        var discardPile = PileType.Discard.GetPile(player);
        var available = discardPile.Cards.Count;
        if (available == 0)
            return;

        // "最多 N 张" → min=0 / max=N：引擎在 min != max 时要求手动确认，选够就能确认，一张不拿也算确认。
        var pickCount = System.Math.Min(DynamicVars.GetRequired<IntVar>("PickCount").IntValue, available);
        var pickPrefs = new CardSelectorPrefs(
            new LocString("cards", "GAOSHOU_BRAIN_SURGE_PICK_PROMPT"), 0, pickCount);
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, discardPile, player, pickPrefs, null)).ToList();
        if (picked.Count != 0)
            await CardPileCmd.Add(picked, PileType.Hand);
    }

    /// <summary>升级：费用 1 -> 0。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /* ===== 历史实现（保留备查）=====
     * 2026-09-18 版：抽 2 张牌，然后选择 1 张手牌放回抽牌堆顶部（升级：移除「消耗」）。
     *     await CardPileCmd.Draw(choiceContext, drawCount, player);
     *     var prefs = new CardSelectorPrefs(prompt, 1, 1);
     *     var chosen = await CardSelectCmd.FromHand(choiceContext, player, prefs, null, this);
     *     foreach (var card in chosen)
     *         await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
     *
     * 更早版本：查看抽牌堆顶部 2 张，选择 1 张加入手牌，其余丢弃
     *     （ModCardVars.Int("LookCount", 2) / ModCardVars.Int("PickCount", 1)，
     *       FromCombatPile + top.Contains 过滤，选中入手、其余弃置）。
     */
}
