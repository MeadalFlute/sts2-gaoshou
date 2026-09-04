using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 急！：技能（稀有）。耗 0 能量 0 星辉。
// 抽到时：额外抽 1 张牌；打出时：将所有星辉变为能量。
// 升级后：第一次打出时，获得 3 点能量。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class PureAnger : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    // 本场战斗是否已触发过"第一次打出"奖励。
    private bool _firstPlayBonusUsed;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/PureAnger.png");

    // 「第一次打出」奖励尚未使用（升级后）时泛橙光。
    protected override bool ShouldGlowGoldInternal => IsUpgraded && !_firstPlayBonusUsed;

    // 星辉/能量图标变量（描述中转换为图标）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Cards", 1),
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Stars", 1),
        // 升级首次打出获得 3 能量的“3”（图标变量=1 配合“3{icon}”显示；数量由该变量驱动，可被外挂修改）。
        ModCardVars.Int("EnergyAmount", 3),
    ];

    public PureAnger() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 星辉：不覆写 CanonicalStarCost，保持默认"无星辉费用"。
    }

    // 抽到时：额外抽 1 张牌（仅当本牌自身被抽到时触发——必须 card==this 守卫，否则每次任意抽牌都+1造成级联抽满手牌）。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this)
            return Task.CompletedTask;
        return CardPileCmd.Draw(choiceContext, DynamicVars.GetRequired<IntVar>("Cards").BaseValue, Owner);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 将所有星辉变为能量：实际给予能量 = 当前星辉 / Stars（每 Stars 点星辉折算 1 点能量）。
        var stars = Owner.PlayerCombatState!.Stars;
        if (stars > 0)
        {
            var ratio = DynamicVars.GetRequired<StarsVar>("Stars").BaseValue;
            if (ratio <= 0m)
                ratio = 1m;   // 除零保护：折算基数为 0 或负数时按 1:1 处理
            var energy = (int)(stars / ratio);   // 向下取整截断到 int（Energy 底层为 int）
            if (energy > 0)
                await PlayerCmd.GainEnergy(energy, Owner);
            Owner.PlayerCombatState.LoseStars(stars);
        }

        // 升级后：第一次打出时，获得 3 点能量。
        if (IsUpgraded && !_firstPlayBonusUsed)
        {
            _firstPlayBonusUsed = true;
            await PlayerCmd.GainEnergy(DynamicVars.GetRequired<IntVar>("EnergyAmount").BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级效果 = 第一次打出时获得 3 能量（见 OnPlay）。
    }
}