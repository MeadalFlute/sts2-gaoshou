using MegaCrit.Sts2.Core.Models.CardPools;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Cards.FreePlay;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 神秘（事件）：技能。耗 0 能量 0 辉星。词条：附赠1、美德、消耗、虚无。
// 附赠：抽到这张牌时，额外抽 1 张牌（同 PureAnger 的 AfterCardDrawn）。
// 美德：打出后获得 1 能量，抽 1 张牌。
// 若回合结束时仍在你的手牌：随机生成 Options(3) 张"已升级"的技能牌，随机抽其中 1 张加入手牌；
// 该牌在本回合结束的弃牌之后仍留在手里，且费用为 0 直到被打出（只作用于这一张实例）。
[RegisterCard(typeof(EventCardPool))]
public sealed class Cryptic : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Event;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Bonus,      // 附赠
        GaoshouKeyword.Virtue,     // 美德
        CardKeyword.Exhaust,       // 消耗
        CardKeyword.Ethereal,      // 虚无
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Bonus", 1),          // 附赠 n：抽到时额外抽 n 张
        ModCardVars.Int("Options", 3),        // 随机生成 3 张候选，再随机取 1 张
        ModCardVars.Int("VirtueEnergy", 1),   // 美德：获得 1 能量
        ModCardVars.Int("VirtueDraw", 1),     // 美德：抽 1 张
    ];

    public Cryptic() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认"无辉星费用"。
    }

    // 回合结束时仍在手牌 → 触发（配合虚无：结算完再被虚无消耗掉）。
    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        var choices = BuildChoices();
        if (choices.Count == 0)
            return;

        // 三选一：抄原版 Discovery 的写法（本角色卡池 + FromChooseACardScreen）。
        // canSkip: false —— 卡面写的是"抽取一张加入手牌"，不给取消。
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: false);
        if (chosen == null)
            return;

        // 「下回合可以免费打出」：必须用 WhenPlayed 过期的 SetUntilPlayed(0)。
        // （SetToFreeThisTurn()/SetThisTurn(0) 的过期标记含 EndOfTurn，会在本回合结束的
        //   CardModel.EndOfTurnCleanup() 里被清掉 —— 老实现"选了牌却没免费"就是这个原因。）
        chosen.EnergyCost.SetUntilPlayed(0);

        // 复用 RitsuLib 现成的"免费出牌"绑定（只是注册表标记，供次级资源结算与其它模组识别；
        // 真正让费用变 0 的是上面那句 SetUntilPlayed(0)）。
        FreePlayBindingRegistry.MarkCardFreeNextPlay(chosen);

        // 这一步发生在本回合"回合结束阶段"、手牌弃置之前，所以要让它活过本次弃牌：
        // 保留判定在 EndOfTurnCleanup 之前执行，所以"单回合保留"足够把它留到下回合。
        chosen.GiveSingleTurnRetain();

        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    /// <summary>
    /// 三张候选：**本角色**卡池里的随机技能牌（原版 Discovery 同款管线 + 确定性 RNG，联机两端一致）。
    /// 早先用的是"所有已解锁角色池 × 只取可升级牌"，既不是本角色池、也不是三选一，与需求不符。
    /// </summary>
    private List<CardModel> BuildChoices()
    {
        var count = (int)DynamicVars.GetRequired<IntVar>("Options").BaseValue;
        if (count <= 0)
            return [];

        var pool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Type == CardType.Skill)
            .ToList();
        if (pool.Count == 0)
            return [];

        var choices = CardFactory
            .GetDistinctForCombat(Owner, pool, count, Owner.RunState.Rng.CombatCardGeneration)
            .ToList();

        // 本牌升级后：三张候选本身就是升级过的技能牌（卡面写的“{IfUpgraded:show:升级过的}”）。
        // 不用 CardCmd.Upgrade —— 它在 CombatManager.IsEnding（本回合已全灭敌人）时会静默跳过；
        // 这两步就是它的核心，且不需要跑 Hook / 播牌堆特效。
        if (IsUpgraded)
        {
            foreach (var candidate in choices.Where(c => c.IsUpgradable))
            {
                candidate.UpgradeInternal();
                candidate.FinalizeUpgradeInternal();
            }
        }

        return choices;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 美德：打出后获得 1 能量，抽 1 张牌。
        var energy = (int)DynamicVars.GetRequired<IntVar>("VirtueEnergy").BaseValue;
        if (energy > 0)
            await PlayerCmd.GainEnergy(energy, Owner);

        var draw = (int)DynamicVars.GetRequired<IntVar>("VirtueDraw").BaseValue;
        if (draw > 0)
            await CardPileCmd.Draw(choiceContext, draw, Owner);
    }

    // 附赠 n：本牌被抽到时额外抽 n 张（必须 card == this 守卫，否则任意抽牌都会级联）。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this)
            return Task.CompletedTask;
        return CardPileCmd.Draw(choiceContext, DynamicVars.GetRequired<IntVar>("Bonus").BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        // 事件牌：升级仅提升附赠数量（1 -> 2）。
        DynamicVars.GetRequired<IntVar>("Bonus").UpgradeValueBy(1);
    }
}
