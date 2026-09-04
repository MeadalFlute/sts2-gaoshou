using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 盾斧（先古）：攻击。耗 1 能量。对所有敌人造成 2 点伤害 4（5）次；获得 1（2）层缓冲。词条：保留。
// 注册在角色卡池（先古卡也归属角色，百科同处“角色池 + 先古”，与原版/参照 LexNinja2 一致）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ChargeBlade : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Ancient;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/ChargeBlade.png");

    // 悬浮释义：缓冲（游戏能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<BufferPower>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("Times", 4),
        ModCardVars.Int("BufferGain", 1),
    ];

    public ChargeBlade() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对所有敌人造成 2 点伤害 4（5）次（焚烧同款：全敌 × N 单次 Execute）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount((int)DynamicVars.GetRequired<IntVar>("Times").BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(this.CombatState)
            .Execute(choiceContext);

        // 获得 1（2）层缓冲。
        await PowerCmd.Apply<BufferPower>(choiceContext, Owner.Creature,
            DynamicVars.GetRequired<IntVar>("BufferGain").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("Times").UpgradeValueBy(1);        // 4 -> 5
        DynamicVars.GetRequired<IntVar>("BufferGain").UpgradeValueBy(1);   // 1 -> 2
    }
}