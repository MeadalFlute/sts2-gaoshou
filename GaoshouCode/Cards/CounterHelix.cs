using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 反击螺旋：技能（罕见）。耗 3 能量 0 辉星。
// 获得 24(30) 格挡；本回合每当你受到一次攻击，立刻对所有敌人反击 5(8) 点伤害
// （结构照原版火焰屏障：反击值 = 能力层数；"本回合"= 敌方回合结束时移除）。
// 伤害是 buff 伤害（不吃力量、不触发荆棘等"被攻击"类敌方能力），且是**一次打全体**的真 AOE。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class CounterHelix : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：计数 buff 本身 + 格挡。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<CounterHelixPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(24m, ValueProp.Move),
        // 每一段反击的伤害（升级 5 -> 8）。
        new DamageVar(5m, ValueProp.Move),
    ];

    public CounterHelix() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 反击值直接作为能力层数传入（原版火焰屏障同款写法）：
        // 层数 = 每次反击的伤害，也就是图标上显示的数字；结算与移除都由能力自己负责。
        await PowerCmd.Apply<CounterHelixPower>(choiceContext, Owner.Creature,
            DynamicVars.Damage.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(6m);    // 24 -> 30
        DynamicVars.Damage.UpgradeValueBy(3m);   // 5 -> 8
    }
}
