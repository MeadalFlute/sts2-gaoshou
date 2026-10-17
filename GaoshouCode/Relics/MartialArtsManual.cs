using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 武林秘籍（稀有）：每当你消耗能量时，获得 1 层临时敏捷；每当你消耗辉星时，获得 1 层临时力量。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class MartialArtsManual : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

        protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 2),
        ModCardVars.Stars("Stars", 2),
    ];


public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 消耗能量 → 临时敏捷；消耗辉星 → 临时力量。
    // 两者**门控完全对称**：每消耗卡面标注的那一点数（`Energy` / `Stars`，默认都是 2）才给 1 层。
    //
    // 【2026-10-15 BUG 修复】旧写法里辉星那条**没有除以阈值**（只要 amount >= 1 就给 1 层）⇒
    //   打一张「1 能量 + 1 辉星」的牌时，能量那条因为不足 2 点正确地没触发，辉星那条却错误地
    //   给了临时力量（用户实测报的"门控出错"）✗
    //   现在两条走同一套 `amount / 阈值` 取整：1+1 ⇒ 两条都是 0 层 ✓；2 点能量 ⇒ 1 层敏捷 ✓；
    //   4 点能量 / 4 点辉星 ⇒ 各 2 层 ✓（阈值直接从卡面变量读，改了数字也不会与代码脱节 ✓）
    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (amount <= 0 || card.Owner != Owner)
            return;
        var per = PerSpendThreshold("Energy");
        var n = amount >= per ? amount / per : 0;
        if (n > 0)
            await GaoshouTemporaryDexterityPower.GrantAsync(new ThrowingPlayerChoiceContext(), Owner.Creature, n, Owner.Creature, card);
    }

    // 消耗辉星 → 临时力量（门控与能量那条一致，见上）。
    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (amount <= 0 || spender != Owner)
            return;
        var per = PerSpendThreshold("Stars");
        var n = amount >= per ? amount / per : 0;
        if (n > 0)
            await GaoshouTemporaryStrengthPower.GrantAsync(new ThrowingPlayerChoiceContext(), Owner.Creature, n, Owner.Creature, null);
    }

    /// <summary>卡面阈值：每消耗这么多点才算一次（变量缺失/为 0 时退化成 1，绝不会除以 0）。</summary>
    private int PerSpendThreshold(string key)
    {
        var value = DynamicVars.TryGetValue(key, out var variable) ? (int)variable.BaseValue : 1;
        return value >= 1 ? value : 1;
    }
}