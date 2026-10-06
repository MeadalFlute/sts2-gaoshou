using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 借用之牌：技能（稀有）。耗 2 能量。选择手牌中至多 2(3) 张牌，将它们的一张复制品加入你的手牌。消耗。
//
// 【2026-10-05 回调】回到"**战斗内临时复制**"（2026-09-24 那版"永久进卡组 + 打出后永久离开卡组"已撤下）：
//   * 复制品用 `CreateClone()` 生成**战斗实例**，再 `AddGeneratedCardToCombat(copy, PileType.Hand)` 入手
//     —— 与幻影（Keywords/Phantom.cs）同一条路径 ✓
//   * 复制品**保持原样**：不减费、不带消耗、不带幻影/回响（不要默认按幻影处理 ✗）
//   * 卡牌本体只有「消耗」；升级只把复制数量 2 → 3（不再有"虚无→移除虚无"）
// ⚠️ `CreateClone()` 必须存在：beta 0.111.0 有 `CreateCloneForPlayer(player)`，正式版 0.107.1 只有
//    `CreateClone()`（见 Entry.cs 的变体自检）⇒ 用两版共有的 `CreateClone()` ✓
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class BorrowedCards : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("CopyCount", 2),
    ];

    public BorrowedCards() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var max = (int)DynamicVars.GetRequired<IntVar>("CopyCount").BaseValue;
        var hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        // 选择至多 max 张手牌（可少选）。**不做任何过滤**：玩家爱复制什么就复制什么（任务牌也允许）。
        var prefs = new CardSelectorPrefs(new LocString("cards", "GAOSHOU_BORROWED_CARDS_PROMPT"), 0, Math.Min(max, hand.Count));
        var selected = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, null, this)).ToList();

        // 每张选中的牌：生成一份**战斗内**复制品加入手牌（同幻影路径；复制品保持原费用/颜色/词条）。
        foreach (var c in selected)
        {
            // 纯防御：选牌结果里已经被移出手牌的，不再复制（避免复制出幽灵牌）。
            if (c?.Pile?.Type != PileType.Hand)
                continue;

            var copy = c.CreateClone();
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner));
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：复制数量 2 -> 3（不再涉及"虚无"）。
        DynamicVars.GetRequired<IntVar>("CopyCount").UpgradeValueBy(1);
    }
}