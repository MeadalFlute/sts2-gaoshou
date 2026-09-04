using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 装弹：状态（无色衍生）。耗 1 能量 1 星辉。使消耗牌堆的 1 张双持冲锋枪（装弹+升级后为双持冲锋枪+）返回手牌。
[RegisterCard(typeof(TokenCardPool))]
public sealed class Reload : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Status;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Reload.png");

    // 悬浮释义：双持冲锋枪（升级后：双持冲锋枪+，从消耗牌堆返回目标卡）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<DualSMG>(IsUpgraded),
    ];

    public override int CanonicalStarCost => 1;

    public Reload() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 从消耗牌堆随机取一张双持冲锋枪返回手牌（装弹+升级后返回 双持冲锋枪+）。
        var dualSmgId = ModelDb.GetId(typeof(DualSMG));
        var exhaust = PileType.Exhaust.GetPile(Owner)?.Cards
            .Where(c => c.Id == dualSmgId).ToList() ?? [];
        if (exhaust.Count == 0)
            return;   // 消耗牌堆没有双持冲锋枪：无事发生。

        var pick = Owner.RunState.Rng.CombatCardSelection.NextItem(exhaust);
        if (IsUpgraded)
            CardCmd.Upgrade(pick);   // 双持冲锋枪+
        await CardPileCmd.Add(pick, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        // 装弹自身无数值升级；升级体现在「返回的是双持冲锋枪+」上（OnPlay 里用 IsUpgraded 判断）。
    }
}