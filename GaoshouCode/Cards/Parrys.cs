using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 招架（Parrys，避免与原版 Parry 重名）：技能（罕见）。耗 0 能量 2 星辉。
// 获得 2(3) 层临时敏捷。奇迹（非回合开始抽牌进入手牌）：击晕一名意图为攻击的敌人。消耗（升级后移除）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Parrys : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private bool _enteredByTurnStartDraw;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 打点：高亮评估入口（临时诊断，验收后删除）。
    // 高亮条件：奇迹可触发（非回合开始抽牌进手）且存在意图攻击的敌人。
    // 高亮：奇迹可触发（非回合开始抽牌进手）且存在意图攻击的敌人。
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            var attackers = (this.CombatState?.HittableEnemies ?? [])
                .Where(e => e.Monster?.NextMove?.Intents.Any(i => i.IntentType == IntentType.Attack) ?? false).ToList();
            var miracle = !_enteredByTurnStartDraw;
            return miracle && attackers.Count > 0;
        }
    }

    // 悬浮释义：击晕。词条联想由 canonical 自动生成。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(MegaCrit.Sts2.Core.HoverTips.StaticHoverTip.Stun),
    ];

    // 词条：奇迹、虚无、消耗（canonical 含消耗以正常渲染描述；升级 tooltip 的消耗由 ManaColorHoverPatch 过滤）。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Miracle,
        CardKeyword.Ethereal,
        CardKeyword.Exhaust,
    ];


    public Parrys() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
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
        Godot.GD.Print($"GAOSHOU-PARRYS-PLAY miracle={!_enteredByTurnStartDraw} enteredByTurnStartDraw={_enteredByTurnStartDraw}");
        // 获得 2 层临时敏捷。
        await GaoshouTemporaryDexterityPower.GrantAsync(choiceContext, Owner.Creature, 2m, Owner.Creature, this);

        // 奇迹（非回合开始抽牌进入手牌）：击晕一名意图为攻击的敌人。
        if (!_enteredByTurnStartDraw)
        {
            var attackers = (this.CombatState?.HittableEnemies ?? [])
                .Where(e => e.Monster?.NextMove?.Intents.Any(i => i.IntentType == IntentType.Attack) ?? false)
                .ToList();
            if (attackers.Count > 0)
            {
                var attacker = Owner.RunState.Rng.CombatTargets.NextItem(attackers);
                if (attacker != null)
                    await CreatureCmd.Stun(attacker);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"消耗"（临时敏捷固定 2）。
        RemoveKeyword(CardKeyword.Exhaust);
        Godot.GD.Print($"GAOSHOU-PARRYS-UP hasexhaust={Keywords.Contains(CardKeyword.Exhaust)}");
    }
}