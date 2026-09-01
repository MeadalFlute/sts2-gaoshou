using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 传说：技能（稀有）。耗 2 能量。从三张其他角色的稀有牌中选择一张结算，重复 2(3) 次。消耗。
// 实现（参考原版 Splash）：每轮给 3 张随机"其他角色卡池"稀有牌，玩家选一张，
// 获得其免费临时复制品（打出后即消失）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Lore : ModCardTemplate
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
        ModCardVars.Int("Times", 3),
    ];

    public Lore() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var times = (int)DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        for (var i = 0; i < times; i++)
        {
            var picked = await RollAndChooseOnce(choiceContext);
            if (picked == null)
                continue;   // 跳过本次选择：后续轮次照常进行
            if (IsUpgraded)
                CardCmd.Upgrade(picked);
            picked.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(picked, PileType.Hand, Owner);
        }
    }

    // 从三张其他角色的稀有牌中选一张（Splash 同款管线）。
    private async Task<CardModel?> RollAndChooseOnce(PlayerChoiceContext choiceContext)
    {
        var pools = Owner.UnlockState.CharacterCardPools.ToList();
        if (pools.Count > 1)
            pools.Remove(Owner.Character.CardPool);

        var candidates = pools
            .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(c => c.Rarity == CardRarity.Rare)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var options = CardFactory.GetDistinctForCombat(Owner, candidates, 3, Owner.RunState.Rng.CombatCardGeneration).ToList();
        if (options.Count == 0)
            return null;

        return await CardSelectCmd.FromChooseACardScreen(choiceContext, options, Owner, canSkip: true);
    }

    protected override void OnUpgrade()
    {
        // 升级：三张选择均改为"升级过的"稀有牌（次数保持 3）。
    }
}