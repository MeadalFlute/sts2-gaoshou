using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
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

// 战斗直觉：能力（稀有）。耗 1 能量。获得 4(6) 层力量、4(6) 层敏捷；你无法再看到敌人的血条和意图。固有。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class BattleInstinct : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：战斗直觉（隐藏意图/血条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<BattleInstinctPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Innate,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("StrengthGain", 3),
        ModCardVars.Int("DexterityGain", 3),
    ];

    public BattleInstinct() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 3(5) 力量 + 3(5) 敏捷。
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("StrengthGain").BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("DexterityGain").BaseValue, Owner.Creature, this);

        // 隐藏敌人血条与意图（能力在敌方回合开始持续重隐）。
        await PowerCmd.Apply<BattleInstinctPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        BattleInstinctPower.HideEnemyUi(Owner.Creature.CombatState);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("StrengthGain").UpgradeValueBy(1);   // 3 -> 4
        DynamicVars.GetRequired<IntVar>("DexterityGain").UpgradeValueBy(1);  // 3 -> 4
    }
}