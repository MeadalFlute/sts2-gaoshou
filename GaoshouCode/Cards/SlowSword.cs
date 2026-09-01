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
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 缓慢剑：技能（罕见）。耗 0 能量 2 星辉。获得 10(15) 层格挡。
// 奇迹（非"回合开始时抽牌"进入手牌）：对随机敌人造成 10(15) 点伤害。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class SlowSword : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private bool _enteredByTurnStartDraw;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override bool GainsBlock => true;

        public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Miracle,
    ];

public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    protected override bool ShouldGlowGoldInternal => !_enteredByTurnStartDraw;

    // 悬浮释义：奇迹（自定义词条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new DamageVar("miracle", 10m, ValueProp.Move),
    ];

    public SlowSword() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    public override int CanonicalStarCost => 2;

    // 记录进入手牌的方式：奇迹仅在"非回合开始时抽牌"的情况下触发。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            _enteredByTurnStartDraw = fromHandDraw;
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // 奇迹：非回合开始时抽牌进入手牌 → 对随机敌人造成 10(15) 点伤害。
        if (!_enteredByTurnStartDraw)
        {
            var enemies = (this.CombatState?.HittableEnemies ?? []).ToList();
            var random = enemies.Count > 0 ? Owner.RunState.Rng.CombatTargets.NextItem(enemies) : null;
            if (random != null)
                await DamageCmd.Attack(DynamicVars.GetRequired<DamageVar>("miracle").BaseValue)
                    .FromCard(this, cardPlay).Targeting(random).Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5);                        // 10 -> 15
        DynamicVars.GetRequired<DamageVar>("miracle").UpgradeValueBy(5); // 10 -> 15
    }
}