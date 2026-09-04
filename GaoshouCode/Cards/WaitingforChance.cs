using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 伺机待发：技能（普通）。耗 1 能量。下回合开始时获得 1 能量、1 星辉、抽 2 张牌。消耗（升级后移除）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class WaitingforChance : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/WaitingforChance.png");

    // 能量/星辉图标变量（描述用 {Energy:energyIcons()}、{Stars:starIcons()}）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Cards", 2),
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    // 消耗：升级后移除。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public WaitingforChance() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 下回合开始时：获得 1 能量、1 星辉、抽 2 张牌（全部使用游戏原生"下回合"buff）。
        await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<StarNextTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级移除"消耗"（直接改实例词条）。
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
