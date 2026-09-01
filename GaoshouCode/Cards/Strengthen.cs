using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 变强：技能（罕见）。耗 0 能量 2 星辉。
// 获得 2(3) 层临时力量和临时敏捷。每次打出一次，这张牌在本场战斗中获得的增益加 2(3)。参考铁甲战士-暴走。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Strengthen : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Green;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Strengthen.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // ① 实时层数 CurrentGain：图鉴=基准2；战斗内 = 基准 + 累计 = GainUp×(N+1) → 2、4、6…
        // ② GainUp 2(3)：第二行"增益加"文本，diff 升级绿标。
        new CalculationBaseVar(2m),
        new CalculationExtraVar(1m),
        new CalculatedVar("CurrentGain").WithMultiplier(static (card, _) =>
            card.DynamicVars.GetRequired<IntVar>("GainUp").BaseValue * PlaysSoFar(card)),
        ModCardVars.Int("GainUp", 2),
    ];

    public override int CanonicalStarCost => 2;

    public Strengthen() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    private int _playsThisCombat;

    public override Task BeforeCombatStart()
    {
        _playsThisCombat = 0;
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 每张变强单独计数：第 N 次打出获得 基准2 + GainUp×(N-1)（与预览一致：2、5、8…）。
        _playsThisCombat++;
        var gain = 2 + (int)DynamicVars.GetRequired<IntVar>("GainUp").BaseValue * (_playsThisCombat - 1);

        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature, gain, Owner.Creature, this);
        await GaoshouTemporaryDexterityPower.GrantAsync(choiceContext, Owner.Creature, gain, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("GainUp").UpgradeValueBy(1);   // 2 -> 3
    }

    private static int PlaysSoFar(CardModel card)
        => card is Strengthen s ? s._playsThisCombat : 0;
}