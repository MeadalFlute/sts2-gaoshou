using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 攻势：技能（罕见 / 多人游戏牌）。耗 1 能量 0 辉星。
//   给予所有玩家 3(5) 层「临时力量」。
// 多人安全：队友枚举与原版 Blade Symphony / Outrage 同款——GetTeammatesOf(含自己) 且只取存活的玩家生物；
//   临时力量必须与等量力量一起给，统一走 GaoshouTemporaryStrengthPower.GrantAsync（内部会先给力量再给临时力量计数）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Onslaught : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    // 多人游戏牌：单人模式下不会出现在卡牌奖励/商店中。
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Onslaught.png");

    // 悬浮释义：临时力量（自定义能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("TemporaryStrength", 3),
    ];

    public Onslaught() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认“无辉星费用”。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        // 原版 Largesse / Blade Symphony / Outrage 同款起手动画。
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        decimal amount = DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue;

        // GetTeammatesOf 含自己；只对存活玩家生物生效（阵亡队友与非玩家生物跳过）。
        foreach (Creature teammate in combatState.GetTeammatesOf(Owner.Creature))
        {
            if (!teammate.IsAlive || !teammate.IsPlayer)
                continue;

            await GaoshouTemporaryStrengthPower.GrantAsync(
                choiceContext, teammate, amount, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 3 -> 5 层。
        DynamicVars.GetRequired<IntVar>("TemporaryStrength").UpgradeValueBy(2);
    }
}
