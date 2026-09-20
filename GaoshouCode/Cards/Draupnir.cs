using MegaCrit.Sts2.Core.Models.CardPools;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Gaoshou.Cards;

// 德罗普尼尔（事件）：技能。耗 1 能量 0 辉星。消耗。
// 获得 10 金币；将 9 张[德罗普尼尔复制]加入你的抽牌堆。
// 不可升级：MaxUpgradeLevel = 0（原版 AscendersBane / Doubt 等诅咒牌的做法），
// 这样升级界面不再出现、CardCmd.Upgrade 也会跳过；文案里以 [gold]不可升级[/gold] 提示玩家。
[RegisterCard(typeof(EventCardPool))]
public sealed class Draupnir : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Event;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    // 不可升级（原版约定：MaxUpgradeLevel 返回 0 即 IsUpgradable 恒为 false）。
    public override int MaxUpgradeLevel => 0;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<DraupnirCopy>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Gold", 10),
        ModCardVars.Int("Copies", 9),
    ];

    public Draupnir() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认"无辉星费用"。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainGold(DynamicVars.GetRequired<IntVar>("Gold").BaseValue, Owner);

        var copies = (int)DynamicVars.GetRequired<IntVar>("Copies").BaseValue;
        for (var i = 0; i < copies; i++)
        {
            var copy = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<DraupnirCopy>(), Owner);
            if (copy == null)
                break;
            CardCmd.PreviewCardPileAdd(
                await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Draw, Owner, CardPilePosition.Random));
        }
    }
}
