using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 连环防御：技能。耗 0 能量 2 星辉。获得 3 格挡、获得 4 格挡。升级后：5、6。
[RegisterCard(typeof(GaoshouCardPool))]
[RegisterCharacterStarterCard(typeof(GaoshouCharacter), 4, Order = 20)]
public sealed class LinkedBlock : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Basic;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/LinkedBlock.png");

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
        new BlockVar("secondBlock", 4m, ValueProp.Move),
    ];

    public LinkedBlock() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 2 星辉：星辉费用通过覆写 CanonicalStarCost 设置。
    public override int CanonicalStarCost => 2;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.GetRequired<BlockVar>("secondBlock"), cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);                             // 3 -> 5
        DynamicVars.GetRequired<BlockVar>("secondBlock").UpgradeValueBy(2m); // 4 -> 6
    }
}
