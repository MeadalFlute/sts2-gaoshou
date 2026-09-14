using MegaCrit.Sts2.Core.Models.CardPools;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 打击格挡奥义（事件）：攻击。耗 3(2) 能量 0 辉星。
// 随机结算 15 张[打击]或[防御]：随机生成 1 张任意角色的、带"消耗"的初始打击/防御牌，然后立即自动打出，重复 15 次。
// 全程用 VakuuCardSelector 接管选牌（自动选），玩家不会被打断，也不会被要求选目标（AutoPlay 的 target=null 会随机选敌）。
[RegisterCard(typeof(EventCardPool))]
public sealed class StrikeDefendMastery : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Event;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Times", 15),
    ];

    public StrikeDefendMastery() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认"无辉星费用"。
    }

    // 候选：所有角色（含模组）里带"打击"或"防御"标签的初始（Basic）牌。
    private static List<CardModel> BuildPool()
    {
        return ModelDb.AllCards
            .Where(c => c.Rarity == CardRarity.Basic
                        && (c.Tags.Contains(CardTag.Strike) || c.Tags.Contains(CardTag.Defend)))
            .ToList();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var times = (int)DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        var pool = BuildPool();
        var combatState = Owner.Creature.CombatState;
        if (pool.Count == 0 || times <= 0 || combatState == null)
            return;

        // 每次独立随机（允许重复），走 RunState 的确定性 RNG，联机两端一致。
        var rng = Owner.RunState.Rng.CombatCardGeneration;

        // 选牌交给 VakuuCardSelector（自动从左到右选满），避免中途弹出"选择一张牌"打断连播。
        using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
        {
            for (var i = 0; i < times; i++)
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;

                var canonical = rng.NextItem(pool);
                if (canonical == null)
                    break;

                // 不能用 CardFactory.GetForCombat：它内部的 FilterForCombat 会把 Rarity == Basic 的牌全部剔除，
                // 而[打击]/[防御]恰恰都是 Basic —— 过滤后集合为空，NextItem 返回 null，随后
                // CombatState.CreateCard(null, owner) 里 canonicalCard.ToMutable() 抛 NullReferenceException，
                // 整条 PlayCardAction 异常 → 卡牌卡在屏幕中间（玩家实测日志已确认这条调用栈）。
                // 所以这里直接在战斗作用域内造牌（等价于原版 AutoPlayFromDrawPile/DualWield 的造牌方式）。
                var card = combatState.CreateCard(canonical, Owner);

                // 带"消耗"：打完进消耗堆。
                card.AddKeyword(CardKeyword.Exhaust);

                // AutoPlay 内部会自己把牌放进 Play 牌堆（card.Pile == null 时），无需先 Add。
                await CardCmd.AutoPlay(choiceContext, card, null);   // target = null → 随机目标
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 3 -> 2
    }
}
