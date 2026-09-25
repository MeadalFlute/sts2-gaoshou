using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 战斗直觉：能力（稀有，**多人游戏专属**）。耗 1 能量。固有。
// 所有玩家（含自己）获得 1(2) 层力量、1(2) 层敏捷；代价是自己无法再看到敌人的血量和意图。
//
// 多人专属靠 CardMultiplayerConstraint.MultiplayerOnly：单人局的 RunState.CardMultiplayerConstraint 是
// SingleplayerOnly，CardPoolModel.GetUnlockedCards 会据此把 MultiplayerOnly 的牌剔出牌池
//（见 MegaCrit.Sts2.Core.Models.CardPoolModel.GetUnlockedCards）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class BattleInstinct : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.AllAllies;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    /// <summary>多人游戏专属：单人对局不会出现。</summary>
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：战斗直觉（隐藏意图/血条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<BattleInstinctPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Innate,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Power<StrengthPower>(2),
        ModCardVars.Power<DexterityPower>(2),
    ];

    public BattleInstinct() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 所有玩家（含自己）1(2) 层力量 + 1(2) 层敏捷。
        // GetTeammatesOf 的语义是"同侧全部生物、包含自身"，和原版 Rally / HuddleUp 完全同写法。
        var teammates =
            from creature in CombatState.GetTeammatesOf(Owner.Creature)
            where creature != null && creature.IsAlive && creature.IsPlayer
            select creature;

        foreach (var teammate in teammates)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, teammate,
                DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<DexterityPower>(choiceContext, teammate,
                DynamicVars["DexterityPower"].BaseValue, Owner.Creature, this);
        }

        // 代价由全场承担：隐藏敌人血条与意图（能力在敌方回合开始持续重隐）。
        // 多人下玩家可以交流，只让施放者看不见等于没代价，所以补丁按"场上任一玩家持有"判断（见 BattleInstinctPatch）。
        await PowerCmd.Apply<BattleInstinctPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        BattleInstinctPower.HideEnemyUi(Owner.Creature.CombatState);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthPower"].UpgradeValueBy(1);   // 1 -> 2
        DynamicVars["DexterityPower"].UpgradeValueBy(1);  // 1 -> 2
    }
}
