using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using Gaoshou.Powers;

namespace Gaoshou.Cards;

// 止水：技能（普通）。耗 0 能量 1 辉星（升级 0 能量 0 辉星）。
// 获得 1 点临时力量；若这是这张牌第一次在本回合被打出，则额外获得 1 点；增幅2(4)。
// 【2026-10-05 底板调整】底板由 1 提到 2（= 首次打出 1 + 额外 1），**斜率保持每弃 1 张 1 层不变**：
//   档位（实际弃置 d 张）= 2、3、4、5、6（升级 4 弃）；旧版 2+2d 的爆炸段（弃 1 就得 4）不再存在。
//   "第一次打出"的判定句式与状态来源**照抄原版「取回 Fetch」**（战斗历史，不引入自有状态）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class StillWater : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：增幅（词条）、临时力量（能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Amplify),
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("AmplifyCount", 2),
        ModCardVars.Int("Strength", 1),
        ModCardVars.Int("FirstPlayBonus", 1),
    ];

    public StillWater() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 辉星（升级 0 能量 0 辉星）。
    public override int CanonicalStarCost => 1;

    // 主效果：授予 amount 层临时力量（同步等量力量）。
    private async Task GrantAsync(PlayerChoiceContext choiceContext, int amount)
    {
        if (amount <= 0)
            return;

        await GaoshouTemporaryStrengthPower.GrantAsync(choiceContext, Owner.Creature, amount, Owner.Creature, this);
    }

    // 增幅重放用的主体：固定只给底板 1 层（额外那 1 层只认"本回合第一次打出"）。
    private Task ReplayOnceAsync(PlayerChoiceContext choiceContext)
        => GrantAsync(choiceContext, (int)DynamicVars.GetRequired<IntVar>("Strength").BaseValue);

    /// <summary>
    /// 「如果这是这张牌第一次在本回合被打出」——判定口径与状态来源**照抄原版「取回 Fetch」**：
    /// 扫一遍**本回合已结算**的出牌记录里有没有这张牌自己（实例比较）。
    /// 战斗历史是全网同步的 ⇒ 多人两端结论一致，不需要我们自建标记（也就没有分歧风险）。
    ///
    /// ⚠️ 必须在增幅循环**之外**只快照一次：本次出牌在重放期间尚未结算进历史，
    ///    若把判定放进被重放的主体里，重放也会被判成"第一次" ⇒ 变成 2+2d 的旧爆炸曲线 ✗
    /// </summary>
    private bool IsFirstPlayThisTurn()
    {
        var history = CombatManager.Instance?.History;
        if (history == null)
            return true;

        foreach (var entry in history.CardPlaysFinished)
        {
            if (entry?.CardPlay == null)
                continue;

            if (!ReferenceEquals(entry.CardPlay.Card, this))
                continue;

            if (entry.HappenedThisTurn(CombatState))
                return false;
        }

        return true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 底板 1 层；"本回合第一次打出此牌"再额外 1 层（先快照，再进增幅循环）。
        var firstPlay = IsFirstPlayThisTurn();
        var grant = (int)DynamicVars.GetRequired<IntVar>("Strength").BaseValue;
        if (firstPlay)
            grant += (int)DynamicVars.GetRequired<IntVar>("FirstPlayBonus").BaseValue;
        await GrantAsync(choiceContext, grant);

        // 增幅2(4)：弃置最多 N 张牌，每弃 1 张重放一次主体（重放只给 1 层）。
        var amplifyCount = (int)DynamicVars.GetRequired<IntVar>("AmplifyCount").BaseValue;
        await this.AmplifyAsync(choiceContext, amplifyCount, ReplayOnceAsync);
    }

    protected override void OnUpgrade()
    {
        // 升级：辉星→0。
        DynamicVars.GetRequired<IntVar>("AmplifyCount").UpgradeValueBy(2);   // 2 -> 4
        // UpgradeStarCostBy(-1);
    }
}
