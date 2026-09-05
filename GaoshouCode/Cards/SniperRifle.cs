using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 狙击枪：技能（罕见）。耗 1 能量 0 星辉。造成当前力量 3 倍的伤害。消耗（升级后获得幻影）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class SniperRifle : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：力量（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
    ];

    // 消耗；升级后追加幻影（在 OnUpgrade 里 AddKeyword，避免 IsUpgraded 惰性缓存问题）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,

        CardKeyword.Exhaust,
    ];

    // 计算伤害：基础 0 + 3 × 当前力量（描述 {CalculatedDamage:diff()} 显示战斗内伤害预览）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new ExtraDamageVar(2m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(
            static (card, target) => card.Owner!.Creature.GetPowerAmount<StrengthPower>()),
    ];

    public SniperRifle() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认“无星辉费用”。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 造成当前力量 3 倍的伤害。
        var strength = Owner.Creature.GetPowerAmount<StrengthPower>();
        var damage = strength * 3m;
        if (damage <= 0)
            return;

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得幻影（直接改实例词条）。
        AddKeyword(GaoshouKeyword.Phantom);
    }
}