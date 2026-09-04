using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 跌宕起伏：技能（普通）。真 X 能量费用（EnergyCost.CostsX = true；自动消耗全部能量），0 星辉。
// 打出时：获得 X 点星辉（升级后 X+1）。保留。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TwistAndTurn : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Stars("Stars", 1),
    ];

    public TwistAndTurn() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 真 X 能量费用：CostsX 置 true（卡面显示 X、打出时自动消耗全部能量）。
        if (!EnergyCost.CostsX)
        {
            var field = typeof(CardEnergyCost).GetField("<CostsX>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(EnergyCost, true);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // X = 本次为 X 费用实际消耗的能量（含 ChemicalX 类修正）；获得 X（升级后 X+1）点星辉。
        var x = ResolveEnergyXValue();
        // 当 X>=2 时，X 本身翻倍（能量消耗翻倍计星辉）；升级 +1 在翻倍后追加。
        if (x >= 2)
            x *= 2;
        var gain = x + (IsUpgraded ? 1 : 0);
        await PlayerCmd.GainStars(gain, Owner);
    }

    protected override void OnUpgrade()
    {
        // 升级：X -> X+1（在 OnPlay 中按 IsUpgraded 处理）。
    }
}