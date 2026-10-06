using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Potions;

// 两仪药水（普通，原名"均衡药水"）：获得 1 能量、2 辉星。
//   * 数值依据：**1E ≈ 1.5S**（作者口径）⇒ 本药水 = 2.33E；原版锚点：能量药水 2E、星星药水 3S = 2.00E
//     ⇒ 两个候选（1E+1S = 1.67E / 1E+2S = 2.33E）对称分布于锚点两侧，取**上侧**：
//       双资源"两种都能用"本身就是卖点，且 1E+1S 常常"打不出什么"（底板太薄 = 体感判废）✓
//   * 参考原版：能量药水（EnergyPotion，普通，2 能量）/ 星星药水（StarPotion，普通，3 辉星）✓
//   * 目标可选任意玩家（多人可给队友）✓ 口径同原版能量药水 ✓
//   * 美术：自制图标 `images/potions/equilibrium_potion.png`（256×256，透明底，画风参照原版药水图集）✓
//     `OutlinePath` 留空即可（可选：药水轮廓图，本模组暂无）✓
[RegisterPotion(typeof(Gaoshou.Characters.GaoshouPotionPool))]
public sealed class Equilibrium : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new StarsVar(2),
    ];

    // ⚠️ ModPotionTemplate 把 ExtraHoverTips 封了 ⇒ 悬浮释义只能走 AdditionalHoverTips ✓
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
    ];

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: $"{Entry.ResPath}/images/potions/equilibrium_potion.png");

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        var player = target!.Player;

        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
        await PlayerCmd.GainStars(DynamicVars.Stars.BaseValue, player);
    }
}
