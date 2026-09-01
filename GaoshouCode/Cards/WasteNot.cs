using MegaCrit.Sts2.Core.Commands;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 可别浪费：能力（罕见）。耗 0 能量 2 星辉。
// 回合开始时，获得 1 张随机「废品牌」；升级后：打出时立即获得 2 张随机废品牌。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class WasteNot : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedPurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/WasteNot.png");

    public WasteNot() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 2;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力：回合开始时获得 1 张随机废品牌。
        await PowerCmd.Apply<DontWastePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 升级后：打出时立即获得 2 张随机废品牌。
        if (IsUpgraded)
            await GenerateWasteAsync(choiceContext, 2);
    }

    protected override void OnUpgrade()
    {
        // 无费用/数值升级；升级效果 = 打出时立即获得 2 张废品牌（见 OnPlay）。
    }

    // 从废品牌池生成 N 张随机废品牌加入手牌（按废品属性过滤，不按池）。
    private async Task GenerateWasteAsync(PlayerChoiceContext choiceContext, int count)
    {
        var candidates = ModelDb.AllCardPools
                .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
                .Where(c => c is IWasteCard)
                .ToList();
        var generated = CardFactory.GetDistinctForCombat(
                Owner,
                candidates,
                count,
                Owner.RunState.Rng.CombatCardGeneration)
            .ToList();
        foreach (var c in generated)
            await CardPileCmd.AddGeneratedCardToCombat(c, PileType.Hand, Owner);
    }
}