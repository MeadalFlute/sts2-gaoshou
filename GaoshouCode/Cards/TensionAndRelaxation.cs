using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 张弛：技能（普通）。耗 0 能量。抽 1 张“红牌”和 1 张“蓝牌”。消耗（升级添加“保留”，暂未实装）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TensionAndRelaxation : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedBlue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/TensionAndRelaxation.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public TensionAndRelaxation() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 红牌 1 张、蓝牌 1 张。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Cards("RedCards", 1),
        ModCardVars.Cards("BlueCards", 1),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawByColorAsync(choiceContext, GaoshouCardColor.Red,
            DynamicVars.GetRequired<CardsVar>("RedCards").IntValue);
        await DrawByColorAsync(choiceContext, GaoshouCardColor.Blue,
            DynamicVars.GetRequired<CardsVar>("BlueCards").IntValue);
    }

    private async Task DrawByColorAsync(PlayerChoiceContext choiceContext, GaoshouCardColor want, int count)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState == null)
            return;

        for (var i = 0; i < count; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                return;

            var hand = PileType.Hand.GetPile(Owner);
            if (hand.Cards.Count >= CardPile.MaxCardsInHand)
                return;
            // 尊重“不可抽牌”debuff（如 NoDrawPower）：与游戏 CardPileCmd.Draw 一致，先判定是否可抽。
            if (!Hook.ShouldDraw(combatState, Owner, false, out var modifier))
            {
                if (modifier != null)
                    await Hook.AfterPreventingDraw(combatState, modifier);
                return;
            }

            var draw = PileType.Draw.GetPile(Owner)?.Cards.ToList() ?? [];
            var candidates = draw.Where(c => MatchesColor(c, want)).ToList();
            if (candidates.Count == 0)
                return;
            var selected = candidates[0];
            await CardPileCmd.Add(selected, PileType.Hand);
            if (selected.Pile?.Type != PileType.Hand)
                continue;

            // Count only successful searches as non-turn-start draws, using the
            // same history/hook/notification order as CardPileCmd.Draw.
            CombatManager.Instance.History.CardDrawn(combatState, selected, false);
            await Hook.AfterCardDrawn(combatState, choiceContext, selected, false);
            selected.InvokeDrawn();
        }
    }

    private static bool MatchesColor(CardModel c, GaoshouCardColor want)
    {
        if (c.GetType().GetProperty("CardColor")?.GetValue(c) is not GaoshouCardColor cc)
            return false;
        return want switch
        {
            GaoshouCardColor.Red => cc is GaoshouCardColor.Red or GaoshouCardColor.RedBlue or GaoshouCardColor.RedPurple or GaoshouCardColor.RedGreen,
            GaoshouCardColor.Blue => cc is GaoshouCardColor.Blue or GaoshouCardColor.RedBlue or GaoshouCardColor.BluePurple or GaoshouCardColor.BlueGreen,
            _ => false,
        };
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
