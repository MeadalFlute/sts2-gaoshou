using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 张弛：技能（普通）。耗 0 能量。抽 1 张“红牌”和 1 张“蓝牌”。消耗（升级添加“保留”，暂未实装）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TensionAndRelaxation : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
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

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DrawByColorAsync(choiceContext, GaoshouCardColor.Red);
        await DrawByColorAsync(choiceContext, GaoshouCardColor.Blue);


    }

    private async Task DrawByColorAsync(PlayerChoiceContext choiceContext, GaoshouCardColor want)
    {
        // 尊重“不可抽牌”debuff（如 NoDrawPower）：与游戏 CardPileCmd.Draw 一致，先判定是否可抽。
        if (!Hook.ShouldDraw(Owner.Creature.CombatState, Owner, false, out var modifier))
        {
            if (modifier != null)
                await Hook.AfterPreventingDraw(Owner.Creature.CombatState, modifier);
            return;
        }

        var draw = PileType.Draw.GetPile(Owner)?.Cards.ToList() ?? [];
        var candidates = draw.Where(c => MatchesColor(c, want)).ToList();
        if (candidates.Count == 0)
            return;
        await CardPileCmd.Add(candidates[0], PileType.Hand);
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
