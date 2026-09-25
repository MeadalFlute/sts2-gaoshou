using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 十三幺：技能（稀有）。耗 1 能量 0 辉星。
// 若手牌中至少有 9 张牌且名称各不相同：将所有手牌设为免费，并从左到右依次打出。
// 参考：静默猎手-子弹时间（手牌免费）+ 低语耳环（从左到右依次自动打出）。
//
// 【2026-09-24 调整】不再用 VakuuCardSelector 代选 —— 结算过程中玩家要做的选择（弃牌 / 选牌）
// 一律停下来等玩家，与原版「倾泻 Cascade」的自动打出同一口径。详见 OnPlay 里的说明。
// 升级后获得"保留"。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ThirteenOrphans : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    // 泛光：手牌中含有 >= 9 种不同卡牌时亮起（触发条件就绪）。
    protected override bool ShouldGlowGoldInternal => GetDistinctHandTypeCount() >= 9;

    // 手牌中（排除十三幺自身 this）不同卡牌类型的种数，供高亮与触发判定共用。
    // 排除 this：十三幺在手牌时若算上自己会让高亮偏高；打出后它会离开手牌，两处需保持一致。
    private int GetDistinctHandTypeCount()
    {
        return Owner?.PlayerCombatState?.Hand.Cards
            .Where(c => !ReferenceEquals(c, this))
            .Select(c => c.GetType()).Distinct().Count() ?? 0;
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public ThirteenOrphans() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认"无辉星费用"。
    }

    // 触发门槛：手牌中至少 9 种不同卡牌。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 条件：手牌中（排除十三幺自身）含有 >= 9 种不同卡牌。
        if (GetDistinctHandTypeCount() < 9)
            return;

        // 打出十三幺后，手牌只剩 9 张（手牌上限 10，十三幺自身已出发）。
        var hand = PileType.Hand.GetPile(Owner);
        var cards = hand.Cards.ToList();   // 快照：只处理打出十三幺时的这批手牌，不处理之后抽上来的新牌。
        if (cards.Count == 0)
            return;

        // 先依次把这批手牌移入结算区（Play 区），锁定它们、脱离手牌变动；
        // 再逐张 AutoPlay 结算。这样上一张结算时，其它牌已在 Play 区，
        // 不会被手牌变化/弃置/消耗/变化影响（快照引用保持有效）。
        // ⇒ 预期副作用：这批牌结算期间**手牌是空的**，所以"从手牌里选一张"的提示会看到空手牌、
        //   直接跳过（就是要这个效果）；但如果某张牌带**抽牌**效果，手牌会重新有牌，
        //   那时的选牌提示就会正常停下来让玩家选（见下面的说明）。
        foreach (var card in cards)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;

            if (card.Pile?.Type != PileType.Play)
                await CardPileCmd.Add(card, PileType.Play);
        }

        // 全部移入结算区后再逐张结算。
        //
        // ⚠️ 这里**故意不**再 Push VakuuCardSelector（自动代选）：
        //   十三幺会一次性结算整手牌，中间任何"弃一张牌 / 选一张牌"都应该**停下来等玩家选**，
        //   与原版「倾泻 Cascade」的自动打出同一口径 —— CardSelectCmd.FromHand 在 Selector == null 时
        //   会 SignalPlayerChoiceBegun 并等本地 UI / 远端选择（反编译 CardSelectCmd.cs:829-863）。
        //   主要场景：这批牌里有抽牌效果 ⇒ 手牌重新有牌 ⇒ 玩家可以从新抽到的牌里自己挑。
        //   （空手牌时 FromHand 会直接返回空列表、不弹提示，所以不会卡住。）
        //   ⚠️ 换目标仍然不弹（AutoPlay 传 target=null ⇒ 随机索敌），与原版自动打出一致。
        foreach (var card in cards)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;

            await CardCmd.AutoPlay(choiceContext, card, null);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后获得"保留"（直接改实例词条）。
        AddKeyword(CardKeyword.Retain);
    }
}