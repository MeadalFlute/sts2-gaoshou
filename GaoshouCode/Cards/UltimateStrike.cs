using MegaCrit.Sts2.Core.Models.CardPools;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 无敌打击（事件）：攻击。耗 3 能量 0 辉星。
// 造成 6(9) 点伤害；你卡组中每有 1 张[打击]，额外攻击 1 次。
// 参考原版 铁甲战士-PerfectedStrike 的"按打击数量缩放"写法（这里缩放的是攻击次数，不是伤害），
// 以及原版 Barrage/Flechettes 的 CalculatedHits 写法（战斗内预览实际攻击次数）。
[RegisterCard(typeof(EventCardPool))]
public sealed class UltimateStrike : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Event;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    // 攻击次数预览：CalculatedHits = CalculationBase(1) + 每张[打击] × CalculationExtra(1)。
    // 为什么用 CalculatedVar 而不是普通 IntVar：IntVar 不覆写 DynamicVar.UpdateCardPreview（基类是空实现，
    // src/Core/Localization/DynamicVars/DynamicVar.cs:136-138），战斗中不会重算；只有 CalculatedVar 会在每次
    // NCard.UpdateVisuals 时重跑 multiplier 并写进 PreviewValue（CalculatedVar.cs:62-89；
    // NCard.cs:430-433 → CardModel.UpdateDynamicVarPreview:1452-1481，牌堆变化/抽牌时都会刷新）。
    // 这正是原版 Flechettes / Barrage 的做法（CalculationBase + CalculationExtra + CalculatedVar("CalculatedHits")）。
    // 描述里用 {InCombat:...|} 包起来：InCombat 由引擎塞进 LocString
    // （CardModel.GetDescriptionForPile，CardModel.cs:1382-1383 = 战斗进行中 且 牌在战斗牌堆），
    // 所以战斗中显示真实攻击次数，图鉴/非战斗牌组界面只显示基础值（伤害 6、每张打击额外 1 次），且不会崩。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new CalculationBaseVar(1m),      // 基础攻击 1 次
        new CalculationExtraVar(1m),     // 每张[打击]额外攻击 1 次
        new CalculatedVar("CalculatedHits").WithMultiplier(static (card, _) =>
            card.Owner?.PlayerCombatState?.AllCards.Count(c => c.Tags.Contains(CardTag.Strike)) ?? 0),
    ];

    public UltimateStrike() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认"无辉星费用"。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 与卡面 {CalculatedHits} 预览同一口径（原版 PerfectedStrike 也是用同一份 DynamicVars 计算）。
        var hits = (int)((CalculatedVar)DynamicVars["CalculatedHits"]).Calculate(cardPlay.Target);
        if (hits < 1)
            hits = 1;   // 兜底：至少攻击 1 次

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);   // 6 -> 9
    }
}
