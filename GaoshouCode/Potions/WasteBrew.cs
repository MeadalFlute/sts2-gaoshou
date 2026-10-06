using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Potions;

// 废品药水（罕见，暂定名）：将 3 张**升级过的**随机【废品】牌加入你的手牌。
//   * 候选口径与「垃圾宝箱 / 临时武器」完全一致：遍历各牌池 + `IWasteCard` 标记接口过滤 ✓
//   * 取牌用 `CardFactory.GetDistinctForCombat`（不重复、受多人约束）✓ 与垃圾宝箱同一条路径
//   * 升级用 `CardCmd.Upgrade`（与「人月神话」同一条路径）；
//     ⚠️ 沿用原版行为：`CombatManager.IsEnding`（本回合已全灭敌人）时会静默跳过升级 —— 已知边界，不额外处理 ✓
//   * 美术：自制图标 `images/potions/waste_potion.png`（256×256，透明底，画风参照原版药水图集）✓
[RegisterPotion(typeof(Gaoshou.Characters.GaoshouPotionPool))]
public sealed class WasteBrew : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("WasteCount", 3),
    ];

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: $"{Entry.ResPath}/images/potions/waste_potion.png");

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        var player = target!.Player;

        var candidates = ModelDb.AllCardPools
            .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c is IWasteCard)
            .ToList();
        if (candidates.Count == 0)
            return;

        var cards = CardFactory.GetDistinctForCombat(
                player,
                candidates,
                DynamicVars.GetRequired<IntVar>("WasteCount").IntValue,
                player.RunState.Rng.CombatCardGeneration)
            .ToList();

        foreach (var card in cards)
        {
            // 先升级再入手：手牌里不会出现一张"看着没升级"的中间态 ✓
            CardCmd.Upgrade(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
    }
}
