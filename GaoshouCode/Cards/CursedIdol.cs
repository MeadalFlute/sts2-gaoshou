using MegaCrit.Sts2.Core.Models.CardPools;
using System.Collections.Generic;
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

// 诅咒神像（事件）：技能。耗 0 能量 0 辉星。
// 失去 3(2) 点最大生命值，获得 5 能量。（失去上限生命的写法参考原版先古牌 Brightest Flame：CreatureCmd.LoseMaxHp）
[RegisterCard(typeof(EventCardPool))]
public sealed class CursedIdol : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Event;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("HpLoss", 3),
        ModCardVars.Energy("Energy", 5),
    ];

    public CursedIdol() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认"无辉星费用"。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.LoseMaxHp(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("HpLoss").BaseValue, true);

        // 能量变量用 Energy 型（描述里以图标呈现），实际数值取 BaseValue。
        var energy = (int)DynamicVars.GetRequired<IntVar>("Energy").BaseValue;
        if (energy > 0)
            await PlayerCmd.GainEnergy(energy, Owner);
    }

    protected override void OnUpgrade()
    {
        // 事件牌：升级减少失去的上限生命（3 -> 2）。
        DynamicVars.GetRequired<IntVar>("HpLoss").UpgradeValueBy(-1);
    }
}
