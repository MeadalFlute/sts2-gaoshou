using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 天机乍泄（Fatal Revelation）：技能（罕见）。耗 0 能量 0 星辉。颜色 R（红）。词条：限制。
// 抽 3 张牌，这些牌在本回合内额外具有「虚无」。升级：多抽 1 张（3 -> 4）。
// 注：类名沿用 FatalRevealation（卡图文件名与存档 id 都挂它），显示名走本地化 title。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class FatalRevealation : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：虚无 + 消耗（原版在卡牌自身词条里会把虚无和消耗成对显示，见 CardModel.cs:982；
    // 我们这两条是"本卡临时给别人加"的词条，所以要自己写全）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Limited,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Cards(3),   // 抽几张（文案 {Cards:diff()}，升级 3 -> 4）
    ];

    public FatalRevealation() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 能量 0 辉星：不覆写 CanonicalStarCost。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner!;

        // 抽 N 张（Draw 的返回值就是"这次真的抽到手的牌"，手牌满时被挡下的不算）。
        var drawn = (await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player)).ToList();

        // 给抽到的牌临时加「虚无」，并登记到本回合结束摘掉（见 EtherealThisTurnRule）。
        // 只处理**原本没有虚无**的牌：否则回合末会把卡牌自身的虚无一起摘掉。
        var marked = new List<CardModel>();
        foreach (var card in drawn)
        {
            if (card.Keywords.Contains(CardKeyword.Ethereal))
                continue;

            CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
            marked.Add(card);
        }

        if (marked.Count != 0)
            EtherealThisTurnRule.Mark(player, marked);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);   // 3 -> 4
    }
}
