using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Potions;

// 封穴散（稀有）：**扔出去的单体控制** —— 击晕一名敌人 1 回合。
//
// 设计依据（照原版"角色专属药水"的分层来）：
//   * 普通 = 角色核心资源的"小份"（本 mod：两仪药水）✓
//   * 罕见 = 机制支撑件（本 mod：拾荒药水）✓
//   * **稀有 = 一次性保命**（原版里只有静默猎手的「罐装幽灵」= 1 层无实体属于这一类）✓
//     —— 而原版 63 个药水里**没有任何一个是击晕**（已逐个核对：无 `CreatureCmd.Stun` 调用）✓
//     本 mod 的「逸散 / 招架+ / 震撼拳」都用同一个 `CreatureCmd.Stun` ⇒ 口径与既有设计一致 ✓
//
// 呈现："砸人"的药水 = `TargetType.AnyEnemy` + **命中特效**（原版毒药水/火焰药水就是这么做的：
//   目标身上放一个冲击特效 NGaseousImpactVfx / NGroundFireVfx）⇒ 本卡用**药粉炸开**的粉尘特效 ✓
//
// ⚠️ 单体而非群体：群体击晕会与「处决」（对已击晕目标直接击杀）组合成"一瓶药水杀首领" ✗
//    单体则与既有的「逸散」同量级（一次性、无费用）✓
[RegisterPotion(typeof(Gaoshou.Characters.GaoshouPotionPool))]
public sealed class AcupointPowder : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyEnemy;

    // 悬浮释义：击晕（与本 mod 的击晕牌一致）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Stun),
    ];

    // 美术：自制图标 `images/potions/acupoint_powder.png`（256×256，透明底，画风参照原版药水图集）✓
    public override PotionAssetProfile AssetProfile => new(
        ImagePath: $"{Entry.ResPath}/images/potions/acupoint_powder.png");

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);

        // 命中特效：淡金色药粉炸开（照原版"砸人"药水的写法）。
        var node = NCombatRoom.Instance?.GetCreatureNode(target);
        if (node != null)
        {
            var vfx = NGaseousImpactVfx.Create(node.VfxSpawnPosition, new Color("f2e0a8"));
            NCombatRoom.Instance!.CombatVfxContainer.AddChildSafely(vfx);
        }

        await CreatureCmd.Stun(target!, null);
    }
}
