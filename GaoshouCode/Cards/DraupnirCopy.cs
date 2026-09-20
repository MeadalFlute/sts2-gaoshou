using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 德罗普尼尔复制（无色/衍生）：技能。耗 0 能量 1 辉星。消耗。
// 获得 5 金币。由德罗普尼尔生成，注册到原版衍生牌池（TokenCardPool），不进高手卡池。
// 不可升级：MaxUpgradeLevel = 0（原版约定，IsUpgradable 恒为 false）。
[RegisterCard(typeof(TokenCardPool))]
public sealed class DraupnirCopy : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    // 不可升级（原版约定：MaxUpgradeLevel 返回 0 即 IsUpgradable 恒为 false）。
    public override int MaxUpgradeLevel => 0;

    // 1 辉星：辉星费用通过覆写 CanonicalStarCost 设置。
    // public override int CanonicalStarCost => 1;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Gold", 5),
    ];

    public DraupnirCopy() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainGold(DynamicVars.GetRequired<IntVar>("Gold").BaseValue, Owner);
    }
}
