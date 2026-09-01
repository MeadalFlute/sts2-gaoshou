using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 速子长矛：攻击（稀有）。X 星辉费用（0 能量）。
// 对随机敌人造成 2 点伤害，获得 1(2) 层临时力量，重复 X 次；本场每触发一次奇迹效果，重复次数再乘 X
// （本场奇迹次数为 0 时本卡不产生效果；参考铁甲战士-扯碎）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TachyonLance : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.RandomEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

        // 悬浮释义：临时力量、奇迹。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("TemporaryStrength", 1),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        // 额外命中数 = X × 本场奇迹次数（预览时 X 用当前星辉近似，打出时用实付 LastStarsSpent）。
        new CalculatedVar("CalculatedHits").WithMultiplier(static (card, _) =>
        {
            var x = card.LastStarsSpent > 0 ? card.LastStarsSpent : (card.Owner?.PlayerCombatState?.Stars ?? 0);
            return x * MiracleCounter.GetMiracleCount(card.Owner!);
        }),
        // 预览用：额外打出次数 = 本场奇迹触发次数（纯计数，无 X 依赖）。
        new CalculatedVar("MiracleCount").WithMultiplier(static (card, _) =>
            MiracleCounter.GetMiracleCount(card.Owner!)),
    ];

    // 虚无（升级后移除）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
    ];

    // X 星辉费用（RitsuLib 原生 X）。
    public override bool HasStarCostX => true;

    public TachyonLance() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 总命中 = X（基础） + X×奇迹（额外打出）。
        var extra = (int)((CalculatedVar)DynamicVars["CalculatedHits"]).Calculate(cardPlay.Target);
        var hits = LastStarsSpent + extra;
        if (hits <= 0)
            return;

        // 逐命中交错循环：每次 攻击（随机索敌）→ 叠 1 层临时力量。
        var enemies = (this.CombatState?.HittableEnemies ?? []).ToList();
        for (var i = 0; i < hits; i++)
        {
            var random = enemies.Count > 0 ? Owner.RunState.Rng.CombatTargets.NextItem(enemies) : null;
            if (random != null)
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(random)
                    .Execute(choiceContext);

            await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature,
                DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"虚无"（临时力量固定 1 层，不再升级加层）。
        RemoveKeyword(CardKeyword.Ethereal);
    }
}