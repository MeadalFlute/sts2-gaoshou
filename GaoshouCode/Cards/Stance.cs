using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 架势：技能（罕见）。耗 1 能量 0 星辉。获得 2 层临时敏捷；下回合开始时获得 2(3) 星辉。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Stance : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：临时敏捷（自定义）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryDexterityPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Stars("Stars", 2),
    ];

    public Stance() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 2 层临时敏捷（同步敏捷）。
        await GaoshouTemporaryDexterityPower.GrantAsync(choiceContext, Owner.Creature, 2m, Owner.Creature, this);

        // 下回合开始时获得 2(3) 星辉（原生"下回合星辉"buff；数量由 Stars 变量驱动）。
        var next = (int)DynamicVars.GetRequired<StarsVar>("Stars").BaseValue;
        await PowerCmd.Apply<StarNextTurnPower>(choiceContext, Owner.Creature, next, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<StarsVar>("Stars").UpgradeValueBy(1);   // 下回合星辉 2 -> 3
    }
}