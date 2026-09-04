using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 太极：技能（稀有）。耗 1 能量 1 星辉（升级 0/0）。每有 1 能量获得 1 星辉，每有 1 星辉获得 1 能量（对称互换）。
// 消耗（升级后保留）。打出与结算参考原版「故障机器人-双倍能量」的资源翻倍模式。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class TaiChi : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.ColorlessBlack;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 消耗；升级后保留（不移除）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    // 用于描述中的能量/星辉图标（单图标）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
    ];

    public TaiChi() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 1 能量 1 星辉（升级 0/0）。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 无护符实测确认：打出前游戏已扣费（SpendResources），OnPlay 时状态为"扣费后"。
        // （此前 3/2→2/2 是因为又减了一次自身费用——费用只扣一次。）
        // 参考「故障机器人-双倍能量」：基于结算时（扣费后）的资源做对称互换——
        //   例 3/2 打 1/1 太极 → 状态 2/1 → +2 星辉 +1 能量 → 3/3。
        var energy = Owner.PlayerCombatState!.Energy;
        var stars = Owner.PlayerCombatState!.Stars;

        // 每点能量获得 1 点星辉；每点星辉获得 1 点能量（对称互换）。
        await PlayerCmd.GainStars(energy, Owner);
        await PlayerCmd.GainEnergy(stars, Owner);
    }

    protected override void OnUpgrade()
    {
        // 升级后费用 1/1 -> 0/0；保留"消耗"（不移除）。
        EnergyCost.UpgradeBy(-1);
        UpgradeStarCostBy(-1);
    }
}