using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using Gaoshou.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 碎纸机（稀有）：你可以在火堆处移除卡牌。
// 实现参考壶铃/铲子（TryModifyRestSiteOptions）+ 空笼（CardSelectCmd.FromDeckForRemoval）。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class Shredder : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner)
            return false;
        options.Add(new ShredRestSiteOption(player));
        return true;
    }
}

// 火堆行动：移除一张卡牌。
public sealed class ShredRestSiteOption : RestSiteOption
{
    public override string OptionId => "SHRED";

    // 名称/描述走游戏 rest_site_ui 分类的 OPTION_SHRED（由本 mod 的 loc 注入）。
    // 注：RestSiteOption 的 Icon/Title/Description 均不可覆写，图标沿用游戏缺省路径（暂无自定义图）。

    public ShredRestSiteOption(Player owner)
        : base(owner)
    {
    }

    // 至少有一张可移除的卡才可用（参考 CookRestSiteOption）。
    public override bool IsEnabled => PileType.Deck.GetPile(Owner).Cards.Any(c => c.IsRemovable);

    public override async Task<bool> OnSelect()
    {
        // 从牌组中选择 1 张卡牌移除（空笼同款选择）。Cancelable=true 显示返回键；取消(空选择)时返回 false，不消耗火堆行动。
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1, 1)
        {
            Cancelable = true,
        };
        var chosen = (await CardSelectCmd.FromDeckForRemoval(Owner, prefs)).ToList();
        if (chosen.Count == 0)
            return false;   // 返回键取消 → 不消耗本次火堆行动（可换选其它行动）
        foreach (var c in chosen)
            await CardPileCmd.RemoveFromDeck(c);
        return true;
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public override Task DoRemotePostSelectVfx()
    {
        return Task.CompletedTask;
    }
}