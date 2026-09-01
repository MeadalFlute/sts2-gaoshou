using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 腾挪：技能（罕见）。耗 0 能量 0 星辉。抽 4 张牌，将最少 2 张牌放回抽牌堆顶。消耗（升级后移除）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Maneuver : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Maneuver.png");

    // 消耗；升级后移除。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("ReturnCount", 2),
    ];

    public Maneuver() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 4, Owner);

        // 让玩家从手牌中选出"最少 2 张"放回抽牌堆顶。
        var returnCount = Math.Max(DynamicVars.GetIntOrDefault("ReturnCount"), 0);
        var hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (hand.Count == 0)
        {
            return;
        }

        var minSelect = Math.Min(returnCount, hand.Count);
        var prefs = new CardSelectorPrefs(new LocString("cards", "GAOSHOU_SHIFT_STEP_PROMPT"), minSelect, hand.Count);
        var selected = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, null, this)).ToList();

        // 放回抽牌堆顶：后加入的会落在最上方，故逆序加，让先选中的牌先被抽到。
        foreach (var c in selected.AsEnumerable().Reverse())
        {
            await CardPileCmd.Add(c, PileType.Draw, CardPilePosition.Top, this, false);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"消耗"（直接改实例词条）。
        RemoveKeyword(CardKeyword.Exhaust);
    }
}