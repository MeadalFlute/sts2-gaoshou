using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 玻璃刀：攻击（无色衍生）。耗 0 能量 0 星辉。使敌人失去 6(12) 点生命（不可格挡、不受力量加成）。消耗。
[RegisterCard(typeof(TokenCardPool))]
public sealed class GlassKnife : ModCardTemplate, Gaoshou.Keywords.IWasteCard
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedGreen;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    // 失去生命：不可格挡、不受力量加成（与「捕捉灵魂」同款命令属性）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move),
    ];

    public GlassKnife() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        // 使敌人失去生命：不可格挡、不受力量加成（与「放血/捕捉灵魂」同款 Unblockable|Unpowered 属性）。
        await CreatureCmd.Damage(choiceContext, cardPlay.Target!, DynamicVars.Damage.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Owner.Creature, this, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);   // 5 -> 9
    }
}
