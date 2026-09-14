using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 游行庆典：技能（稀有 / 多人游戏牌）。耗 1 能量 0 辉星。消耗。
//   从三张稀有牌中选一张，加入所有玩家的手牌，这些牌本回合可以免费打出。
//   卡池 = 所有角色的稀有牌混合池（含高手自己，区别于「传说」的「其他角色」）。
//   升级效果：展示前把三张选项升级（同原版 Splash / 本模组的「传说」）。
// 多人安全：选择由施放者在选择界面完成（CardSelectCmd 内部走 PlayerChoiceSynchronizer 同步）；
//   其余玩家各拿一份克隆（CreateCloneForPlayer，参考原版 Outrage），施放者直接用被选中的实例（参考原版 Splash）；
//   队友枚举与原版 Blade Symphony / Outrage 同款——GetTeammatesOf(含自己) 且只取存活的玩家生物。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Parade : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private const int OptionCount = 3;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    // 多人游戏牌：单人模式下不会出现在卡牌奖励/商店中。
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public Parade() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        // 原版 Largesse / Blade Symphony / Outrage 同款起手动画。
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        CardModel? picked = await RollAndChoose(choiceContext);
        if (picked == null)
            return;   // 跳过选择：不给任何玩家发牌

        // GetTeammatesOf 含自己；只对存活玩家生物生效（阵亡队友与非玩家生物跳过）。
        foreach (Creature teammate in combatState.GetTeammatesOf(Owner.Creature))
        {
            if (!teammate.IsAlive || !teammate.IsPlayer || teammate.Player is not { } player)
                continue;

            // 施放者直接用被选中的实例；其余玩家各拿一份克隆（克隆保留升级状态）。
            CardModel copy = ReferenceEquals(player, Owner) ? picked : picked.CreateCloneForPlayer(player);
            copy.SetToFreeThisTurn();
            CardPileAddResult added = await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);

            // 别人收到的牌不会自动播放入手动画——给施放者预览一下（原版 Outrage 同款）。
            if (!ReferenceEquals(player, Owner))
                CardCmd.PreviewCardPileAdd(added, 2.2f);

            // 多人依次到账的节奏（原版 Blade Symphony 同款）。
            await Cmd.Wait(0.1f);
        }
    }

    // 三张"所有角色"的稀有牌；本卡升级时先升级所有选项再展示（选择界面预览即为升级版）。
    private async Task<CardModel?> RollAndChoose(PlayerChoiceContext choiceContext)
    {
        var candidates = Owner.UnlockState.CharacterCardPools
            .SelectMany(pool => pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(c => c.Rarity == CardRarity.Rare)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var options = CardFactory
            .GetDistinctForCombat(Owner, candidates, OptionCount, Owner.RunState.Rng.CombatCardGeneration)
            .ToList();
        if (options.Count == 0)
            return null;

        if (IsUpgraded)
            foreach (CardModel option in options)
                CardCmd.Upgrade(option);

        return await CardSelectCmd.FromChooseACardScreen(choiceContext, options, Owner, canSkip: true);
    }

    protected override void OnUpgrade()
    {
        // 升级效果 = 展示前把三张选项升级（见 RollAndChoose），无需改数值。
    }
}
