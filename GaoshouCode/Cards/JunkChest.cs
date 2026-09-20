using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 垃圾宝箱：技能（罕见）。耗 0 能量 1 辉星。颜色 P（紫）。
// 从 5 张废品牌中选择最多 2（3）张加入手牌。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class JunkChest : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("OfferCount", 5),   // 亮出几张废品牌备选
        ModCardVars.Int("PickCount", 2),    // 最多拿几张（升级 3）
    ];

    // 悬浮释义：逐个预览所有废品牌（滚轮切换 / 2 秒自动轮播，见 Cards/WastePreview.cs）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => WastePreview.Build();

    public JunkChest() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 辉星。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 候选 = 各牌池里的「废品牌」（按 IWasteCard 属性过滤，而非按牌池；写法对齐本模组 ImprovisedWeapon）。
        var candidates = ModelDb.AllCardPools
            .SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(c => c is IWasteCard)
            .ToList();
        if (candidates.Count == 0)
            return;

        var offer = CardFactory.GetDistinctForCombat(
                player,
                candidates,
                DynamicVars.GetRequired<IntVar>("OfferCount").IntValue,
                player.RunState.Rng.CombatCardGeneration)
            .ToList();
        if (offer.Count == 0)
            return;

        // "最多 N 张" → min=0 / max=N：引擎在 min != max 时要求手动确认，选够就能确认，一张不拿也算确认。
        var pickCount = System.Math.Min(DynamicVars.GetRequired<IntVar>("PickCount").IntValue, offer.Count);
        var prefs = new CardSelectorPrefs(new LocString("cards", "GAOSHOU_JUNK_CHEST_PROMPT"), 0, pickCount);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, offer, player, prefs)).ToList();

        foreach (var card in chosen)
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("PickCount").UpgradeValueBy(1);   // 2 -> 3
    }
}
