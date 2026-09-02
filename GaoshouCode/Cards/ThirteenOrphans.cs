using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 十三幺：技能（稀有）。耗 1 能量 0 星辉。
// 若手牌中至少有 9 张牌且名称各不相同：将所有手牌设为免费，并从左到右依次打出。
// 参考：静默猎手-子弹时间（手牌免费）+ 低语耳环（从左到右依次自动打出）。
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
    protected override bool ShouldGlowGoldInternal =>
        (Owner?.PlayerCombatState?.Hand.Cards.Select(c => c.GetType()).Distinct().Count() ?? 0) >= 9;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public ThirteenOrphans() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认"无星辉费用"。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 条件：手牌中含有 >= 9 种不同卡牌。
        var distinctCount = Owner.PlayerCombatState!.Hand.Cards.Select(c => c.GetType()).Distinct().Count();
        if (distinctCount < 9)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        var cards = hand.Cards.ToList();   // 从左到右 = 手牌顺序
        var combat = Owner.Creature.CombatState;
        if (cards.Count == 0 || combat == null)
            return;

        // 低语耳环式自动打出：PushSelector 保证自动索敌、阻断玩家输入。
        using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
        {
            foreach (var card in cards)
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!card.CanPlay())
                    continue;

                card.SetToFreeThisTurn();                  // 子弹时间：手牌免费
                var target = GetAutoTarget(card, combat);
                await card.SpendResources();
                await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, skipXCapture: true);
            }
        }
    }

    /// <summary>
    /// 自动索敌（对齐低语耳环）：敌人取最左存活敌人；友方/玩家取自己；其余为 null。
    /// </summary>
    private static Creature? GetAutoTarget(CardModel card, ICombatState combatState)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
            TargetType.AnyAlly or TargetType.AnyPlayer => card.Owner?.Creature,
            _ => null,
        };
    }

    protected override void OnUpgrade()
    {
        // 升级后获得"保留"（直接改实例词条）。
        AddKeyword(CardKeyword.Retain);
    }
}