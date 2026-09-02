using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 龙卷风摧毁停车场：攻击（稀有）。耗 1 能量 1 星辉（升级 0/1）。对所有敌人造成 3 点伤害，获得 2 点临时力量。
// 风暴（能量、星辉）：打出后，若能量≥1 且 星辉≥1，则额外打出一次。
// 增幅2 额外打出的每一击也判定风暴（可再额外打出一次）；风暴重放本身不触发增幅、不再判定风暴。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TornadoDicimatesTrailerPark : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedPurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/TornadoDicimatesTrailerPark.png");

    // 悬浮释义：增幅（词条）+ 临时力量（能力）。消耗/风暴由关键词自带释义。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Amplify),
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    // 词条：消耗（保留关键词以兼容"移除消耗"类遗物）、风暴。增幅2 写入描述文本。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        GaoshouKeyword.Storm,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("TemporaryStrength", 1),
        ModCardVars.Int("AmplifyCount", 1),
        ModCardVars.Energy("EnergyA", 1),
        ModCardVars.Energy("EnergyB", 1),
        ModCardVars.Stars("StarA", 1),
        ModCardVars.Stars("StarB", 1),
    ];

    public TornadoDicimatesTrailerPark() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 主效果（一次打出 = 全体伤害 + 临时力量）。
    private async Task MainOnceAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 全体伤害（真 AOE：一次攻击同时作用于所有敌人）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(this.CombatState)
                .Execute(choiceContext);

        // 获得 2 点临时力量（同步等量力量）。
        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("TemporaryStrength").BaseValue, Owner.Creature, this);
    }

    // 风暴条件：当前能量≥1 且 星辉≥1。
    private bool StormActive()
        => Owner.PlayerCombatState!.Energy >= 2 && Owner.PlayerCombatState.Stars >= 2;

    /// <summary>
    /// 一次"可风暴"的打出：先执行主效果；若满足风暴条件，再额外打出一次主效果。
    /// 重放的那次不触发增幅、也不再次判定风暴（避免递归）。
    /// </summary>
    private async Task PlayStormableAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await MainOnceAsync(choiceContext, cardPlay);
        if (StormActive())
            await MainOnceAsync(choiceContext, cardPlay);
    }

    // 1 能量 1 星辉（无升级费用变化）。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 本体打出（可风暴重放）。
        await PlayStormableAsync(choiceContext, cardPlay);

        // 增幅 1（2）：弃置最多 AmplifyCount 张牌，然后按实际弃置数重放——每一击也各自判定风暴。
        var amplifyCount = (int)DynamicVars.GetRequired<IntVar>("AmplifyCount").BaseValue;
        await this.AmplifyAsync(choiceContext, amplifyCount, ctx => PlayStormableAsync(ctx, cardPlay));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("AmplifyCount").UpgradeValueBy(1);   // 1 -> 2（费用不变）
    }
}
