using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 轮椅（先古）：每回合开始时，获得 3 星辉；能量不足时，你可以使用星辉支付卡牌的能量。
// 获取方式：欧罗巴斯之触把高手护符升级为轮椅（GaoshouAmulet 上的 RegisterTouchOfOrobasRefinement）。
// 注册回角色遗物池以便百科-遗物大全正确显示与查看描述。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class Wheelchair : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
    [
        ModCardVars.Stars("Stars", 1),
        ModCardVars.Energy("Energy", 1),
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 每场战斗开始：给装备者挂"星辉代付"能力（ShouldPayExcessEnergyCostWithStars 只查询战斗内模型，遗物本体不在其列）。
    public override async Task BeforeCombatStart()
    {
        await PowerCmd.Apply<WheelchairStarPayPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;
        await PlayerCmd.GainStars(3, player);
    }
}