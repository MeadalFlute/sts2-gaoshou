using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 双持冲锋枪：攻击（稀有）。耗 1 能量 1 星辉。对随机敌人造成 2(3) 点伤害，重复 4(6) 次。消耗。风暴（红&蓝）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class DualSMG : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AllEnemies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedBlue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：装弹（变化目标卡）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Reload>(),
    ];

    // 词条：风暴、消耗（打出后生成装弹加入抽牌堆）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Storm,
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("Times", 3),
        ModCardVars.Energy("EnergyA", 1),
        ModCardVars.Stars("StarA", 1),
    ];

    public DualSMG() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 风暴：重放 1。用 OnPlay 内部执行两次代替 BaseReplayCount——
    // 后者依赖 AfterCreated 设置，多人远端克隆不会执行会导致状态分歧。
    private async Task PlayOnce(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var times = DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        foreach (var enemy in this.CombatState?.HittableEnemies ?? [])
        {
            for (var i = 0; i < times; i++)
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(enemy)
                    .Execute(choiceContext);
            }
        }
    }

    // 1 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 依次攻击每个敌人（保留的演出：逐敌、逐次命中）。
        await PlayOnce(choiceContext, cardPlay);

        // 风暴（能量、星辉）：当前能量>=1 且 星辉>=1 才重复打出一次。
        if (Owner.PlayerCombatState!.Energy >= 1 && Owner.PlayerCombatState.Stars >= 1)
            await PlayOnce(choiceContext, cardPlay);

        // 将一张装弹加入你的抽牌堆（本卡照常进消耗）。
        var reload = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<Reload>(), Owner);
        if (reload != null)
            await CardPileCmd.Add(reload, PileType.Draw);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("Times").UpgradeValueBy(1);    // 3 -> 4（伤害不变）
    }
}
