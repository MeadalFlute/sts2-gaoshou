using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 这个顺手（能力）：每当你打出一张【临时】牌后，对随机敌人造成 Amount 点伤害。
// 临时机制：打出的牌带「临时」词条（当前幻影复制品等）即触发。
[RegisterPower]
public sealed class ThisHandyPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/thishandy.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/thishandy.png");

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        // 临时牌 = 局内生成、不属于牌组（DeckVersion == null）。仅响应自己的打出（多人防串触发）。
        if (player == null || player != Owner.Player || cardPlay.Card.DeckVersion != null)
            return;

        var enemies = player.Creature.CombatState?.HittableEnemies.ToList() ?? [];
        if (enemies.Count == 0)
            return;

        var enemy = player.RunState.Rng.CombatTargets.NextItem(enemies)!;
        await CreatureCmd.Damage(choiceContext, enemy, Amount, ValueProp.Unpowered, player.Creature, cardPlay.Card, cardPlay);
    }
}