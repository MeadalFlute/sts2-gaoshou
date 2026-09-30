using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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

// 垃圾喷射器-装填：技能（稀有）。耗 3 能量 0 辉星。消耗。
// 效果：将**所有「临时牌」打出并消耗**；每以此法消耗一张，获得 5(6) 点格挡；
//       将一张「垃圾喷射器」加入你的手牌，其攻击次数 = 本次消耗的临时牌数量。
//
// 「临时牌」的判定（【2026-09-29 修正】）：**本 mod 的权威定义 = 局内生成、不属于牌组**
//   ⇒ `card.DeckVersion == null`（见 Powers/ThisHandyPower.cs:28-29 的注释与判定）。
//   ⚠️ 之前用自定义词条 GaoshouKeyword.Temporary 判定是错的 ✗：全 mod 没有任何牌会挂该词条
//      （它只在 Cards/ThatsHandy.cs:33、Cards/Spark.cs:34 的悬浮释义里被引用）⇒ 一张都找不到 ⇒ 实机"没反应" ✗。
//
// 「次数 + 回手牌」的传递照 DualSMG <-> Reload 那一对（Cards/DualSMG.cs:113-127 写 PairId、
// Cards/Reload.cs:89-106 按 PairId 精确找回）：
//   * 本卡生成「垃圾喷射器」时，给它写 Times = 本次消耗数量，并给"这张装填"和"那张喷射器"写同一个 PairId；
//   * 喷射器结算时按 PairId 在**消耗牌堆**里找回"生成它的那张装填"并放回手牌（Cards/RockItLauncher.cs）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class RockItLauncherReload : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    // 卡牌颜色 N（无色）。
    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：预览它生成的「垃圾喷射器」（升级后预览「垃圾喷射器+」，写法照 LossAversion.cs:35）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<RockItLauncher>(IsUpgraded),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每以此法消耗一张临时牌获得的格挡（5 -> 6）。
        new BlockVar("BlockPer", 5m, ValueProp.Move),
        // 配对编号的存储位（生成喷射器时与它写同一个数；卡面不引用，所以不显示）。
        ModCardVars.Int("PairId", 0),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public RockItLauncherReload() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1) 取牌 + **先确定数量**（格挡要按它算，见第 2 步）
        //    本 mod 对「临时牌」的权威定义 = **局内生成、不属于牌组**（DeckVersion == null）
        //      —— Powers/ThisHandyPower.cs:28-29 的注释与判定就是这条；取牌范围沿用散射炮
        //      FlakCannon.cs:48-51 的口径 PlayerCombatState.AllCards（= 所有战斗牌堆）。
        //    ⚠️【2026-09-29 修"卡在屏幕中间"】还必须再排除两种：
        //      * **本卡自己**（ReferenceEquals(c, this)）：装填是衍生牌（DeckVersion == null），
        //        不排除就会在结算途中把自己也当成"临时牌"再打一次 ⇒ 递归 ✗；
        //      * **已经在结算区 Play 的牌**：它们正在结算，重复 AutoPlay 会让这次出牌永远收不了尾 ✗。
        //      日志实证（godot.log L1907-1956）：`找到临时牌 1 张: ...ROCK_IT_LAUNCHER_RELOAD@Play` +
        //      `第 1 张处理完: ...ROCK_IT_LAUNCHER_RELOAD -> pile=Play` 连续约 20 次 ⇒ 自递归 ✗
        //      ⇒ 出牌流程卡住 ⇒ 卡面停在屏幕中间 ✗。
        var temporary = (Owner.PlayerCombatState?.AllCards ?? [])
            .Where(c => c.DeckVersion == null
                        && !ReferenceEquals(c, this)
                        && c.Pile?.Type != PileType.Exhaust
                        && c.Pile?.Type != PileType.Play)
            .ToList();

        // 【临时调试日志】确认"打出了/消耗了"用（定位完可删）：

        // 2) 【2026-09-29 用户要求调整顺序，第二版】**先获得格挡**，再处理这些临时牌。
        //    格挡按**最终数量**算 ⇒ 数量必须在上面先快照确定（这里用 planned）✓。
        var planned = temporary.Count;
        if (planned > 0)
        {
            var per = DynamicVars.GetRequired<BlockVar>("BlockPer").BaseValue;
            await CreatureCmd.GainBlock(Owner.Creature, new BlockVar(planned * per, ValueProp.Move), cardPlay);
        }

        // 3) 【2026-09-29 用户要求：照十三幺的结构】**先把所有临时牌一次性移入结算区（Play）**，
        //    再逐张结算。两个循环的结构与 Cards/ThirteenOrphans.cs:71-95 完全一致：
        //      * 第一个循环：只搬牌（IsOverOrEnding 守卫 + `if (card.Pile?.Type != PileType.Play) await CardPileCmd.Add(card, PileType.Play);`）；
        //      * 第二个循环：才逐张结算（ThirteenOrphans 那里只有 AutoPlay；我们按「破灭 Havoc」的
        //        CardPileCmd.AutoPlayFromDrawPile（betaTest CardPileCmd.cs:1168-1175）在 AutoPlay 前
        //        多设 `card.ExhaustOnNextPlay = true`，事后再补一次 CardCmd.Exhaust 兜底 = 散射炮 FlakCannon.cs:38-41 的写法）。
        //    ⚠️ 第二个循环里**不再**搬牌（移入已在第一个循环做完，与 ThirteenOrphans 一致）。
        foreach (var card in temporary)
        {
            if (CombatManager.Instance.IsOverOrEnding)
            {
                break;
            }

            if (card.Pile?.Type != PileType.Play)
                await CardPileCmd.Add(card, PileType.Play);
        }


        // 4) 逐张结算（顺序不变：设 ExhaustOnNextPlay → AutoPlay 完整结算 → 兜底 Exhaust）。
        var consumed = 0;
        foreach (var card in temporary)
        {
            if (CombatManager.Instance.IsOverOrEnding)
            {
                break;
            }

            card.ExhaustOnNextPlay = true;

            await CardCmd.AutoPlay(choiceContext, card, null);

            if (card.Pile?.Type != PileType.Exhaust)
                await CardCmd.Exhaust(choiceContext, card);

            consumed++;
        }


        // 5) 生成一张「垃圾喷射器」加入手牌：次数 = 本次消耗数量，并双向写 PairId（供它把本卡拿回手牌）。
        var launcher = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<RockItLauncher>(), Owner);
        if (launcher != null)
        {
            // 升级态传递（照 Cards/LossAversion.cs:62-63 的写法）：本卡升级后，生成的喷射器也应是升级版。
            if (IsUpgraded)
                CardCmd.Upgrade(launcher);

            var pairId = NextPairId();
            DynamicVars.GetRequired<IntVar>("PairId").BaseValue = pairId;
            launcher.DynamicVars.GetRequired<IntVar>("PairId").BaseValue = pairId;
            launcher.DynamicVars.GetRequired<IntVar>("Times").BaseValue = consumed;
        }

        if (launcher != null)
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(launcher, PileType.Hand, Owner));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<BlockVar>("BlockPer").UpgradeValueBy(1);   // 5 -> 6
    }

    // 本场战斗内递增的配对编号（与 Cards/DualSMG.cs:134-138 同一做法：两端在同一出牌点各自 +1，结果一致；
    // 只在"同一实例对"之间比较，不需要跨战斗唯一）。
    private static int _nextPairId = 1;

    private static int NextPairId() => _nextPairId++;
}
