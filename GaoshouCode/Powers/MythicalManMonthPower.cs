using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Powers;

// 人月神话（能力）：**本场战斗中临时加入你牌组/牌堆的牌会被升级**。
//
// 实现来源（用户指定参考的 Watcher mod）：
//   资料\Watcher\WatcherMod\MasterRealityPower.cs:16-24 —— 它 override
//   `Task AfterCardGeneratedForCombat(CardModel card, Player? creator)`，在
//   `card.Owner.Creature == Owner && card.IsUpgradable` 时调 `CardCmd.Upgrade(card, ...)`。
// 钩子本身来自原版：`Hook.AfterCardGeneratedForCombat(ICombatState, CardModel, Player?)`
//   见游戏源码 src\Core\Hooks\Hook.cs:251-258（逐个战斗监听者调用
//   `AbstractModel.AfterCardGeneratedForCombat(card, creator)`）。
//   ⇒ 凡是"生成一张牌进战斗"的路径（如 CardPileCmd.AddGeneratedCardToCombat）都会走到这里，
//     包括本 mod 自己的 装填/喷射器/废品牌 生成，符合"临时加入牌组的牌会被升级"的口径。
//
// ⚠️ 已知边界（沿用原版行为，不额外处理）：CardCmd.Upgrade 在 CombatManager.IsEnding（本回合已全灭敌人）时会静默跳过
//   —— 本仓库 Cards/Cryptic.cs:112 有同样的记录；这里与 Watcher 参考实现保持一致。
[RegisterPower]
public sealed class MythicalManMonthPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 图标：用户提供的 buff 图标（cardart_out\bufficon\全知.png ⇒ images/powers/mythicalmanmonth.png，已 import）。
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/mythicalmanmonth.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/mythicalmanmonth.png");

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只升级"属于本能力拥有者"的牌；不可升级的牌（MaxUpgradeLevel == 0 => IsUpgradable false）跳过。
        if (card.Owner?.Creature == Owner && card.IsUpgradable)
            CardCmd.Upgrade(card);

        return Task.CompletedTask;
    }
}
