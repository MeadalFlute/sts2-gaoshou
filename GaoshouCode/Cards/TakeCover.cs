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

// 寻找掩护：技能（普通）。耗 2 能量。获得 11(15) 层格挡；获得 1(2) 辉星。
// 【2026-10-05 时序调整】辉星由"下回合开始"改为**当回合立即**获得：蓝色防御牌不再与后续蓝牌抢同一库存星辉。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TakeCover : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(11m, ValueProp.Move),
        ModCardVars.Stars("Stars", 1),
    ];

    public TakeCover() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 当回合立即获得 1(升级2) 辉星（数量由 Stars 变量驱动）。
        var starsGain = (int)DynamicVars.GetRequired<StarsVar>("Stars").BaseValue;
        await PlayerCmd.GainStars(starsGain, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4);   // 11 -> 15
        DynamicVars.GetRequired<StarsVar>("Stars").UpgradeValueBy(1);   // 辉星 1 -> 2
    }
}