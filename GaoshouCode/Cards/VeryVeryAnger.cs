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
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 生气气：技能（普通）。耗 1 能量 0 星辉（升级后能量 1->0）。
// 恢复 2 点生命，获得 2 层临时力量。词条：风暴。消耗。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class VeryVeryAnger : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/VeryVeryAnger.png");

    // 悬浮释义：临时力量（能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    // 词条：风暴（自定义，可悬停显示释义）+ 消耗（游戏原生词条）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Storm,
        CardKeyword.Exhaust,
    ];

    // 风暴条件图标变量（能量≥2：两个能量图标）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("HealGain", 3),

        ModCardVars.Energy("EnergyA", 1),
        ModCardVars.Energy("EnergyB", 1),
    ];

    public VeryVeryAnger() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认“无星辉费用”。
    }

    // 风暴：重放 1。用 OnPlay 内部执行两次代替 BaseReplayCount——
    // 后者依赖 AfterCreated 设置，多人远端克隆不会执行会导致状态分歧。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 回复 3(5) 点生命。
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.GetRequired<IntVar>("HealGain").BaseValue);

        // 获得 1 层力量（普通力量，非临时）。
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 风暴（红红）：当前能量>=2 才重放。
        if (Owner.PlayerCombatState!.Energy >= 2)
        {
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.GetRequired<IntVar>("HealGain").BaseValue);
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("HealGain").UpgradeValueBy(2);   // 回复 3 -> 5
        // 费用保持 2。
    }
}
