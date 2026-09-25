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

// 借用之牌：技能（稀有）。耗 2 能量。选择手牌中至多 2 张牌，将它们的一张复制品**永久加入你的卡组**；
// 打出后**永久离开你的卡组**。消耗、虚无（升级移除虚无）。
//
// 【2026-09-24 重做】原效果是"把一张**临时**复制品加入**手牌**"（幻影同款 CreateClone + AddGeneratedCardToCombat）；
// 现在改成永久进卡组：
//   * 克隆源取手牌的 `DeckVersion`（= 卡组侧那一份原牌）、用 `RunState.CloneCard` 生成**运行层**克隆，
//     再 `CardPileCmd.Add(copy, PileType.Deck)`（与多莉的镜子 DollysMirror 同一口径）；
//   * "打出后永久离开卡组"照 DeprecatedCard 的写法：`RemoveFromDeck(DeckVersion)` 后清掉引用。
//
// ⚠️ 关键前提（反编译 Player.PopulateCombatState 核实）：战斗开始时，游戏把卡组每张牌
// `state.CloneCard(deckCard)` 克隆进抽牌堆，并让**战斗克隆**的 `DeckVersion` 指向卡组里那张原牌。
// 所以战斗中任何"卡组侧"操作都必须走 `DeckVersion`：
//   * 不能把 `this` 直接丢给 `RemoveFromDeck`（它要求 `Pile.Type == PileType.Deck`，而 this 在 Play 区 ⇒ 抛异常）；
//   * 手牌里现生成的牌（token/复制品）没有 DeckVersion ⇒ 复制它们时退回手牌实例本身，且它们本来就不在卡组里、
//     所以不会误删任何东西 ✓。
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
        CardKeyword.Ethereal,
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
        if (hand.Count > 0)
        {
            // 选择至多 max 张手牌（可少选）。**不做任何过滤**：玩家爱复制什么就复制什么（任务牌也允许）。
            var prefs = new CardSelectorPrefs(new LocString("cards", "GAOSHOU_BORROWED_CARDS_PROMPT"), 0, Math.Min(max, hand.Count));
            var selected = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, null, this)).ToList();

            // 每张选中的牌：生成一份复制品**永久加入卡组**（不再放进手牌）。
            foreach (var c in selected)
            {
                var source = c.DeckVersion ?? c;
                var copy = Owner!.RunState.CloneCard(source);
                CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(copy, PileType.Deck));
            }
        }

        // 打出后永久离开你的卡组（卡组里那一份是 DeckVersion；已经不在卡组里时什么都不做）。
        if (this.DeckVersion != null)
        {
            await CardPileCmd.RemoveFromDeck(this.DeckVersion);
            this.DeckVersion = null;
        }
    }

    protected override void OnUpgrade()
    {
        // 复制数量固定 2；升级后移除"虚无"。
        RemoveKeyword(CardKeyword.Ethereal);
    }
}