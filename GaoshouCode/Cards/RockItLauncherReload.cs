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

// 垃圾喷射器-装填：技能（稀有）。耗 2 能量 0 辉星。消耗。
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
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    // 卡牌颜色 N（无色）。
    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：①「临时」词条（卡面第一行「打出并消耗所有临时牌」里的那个词 ——
    //   2026-10-12 用户反馈"hovertips 缺少临时牌" ⇒ 补上；同款写法见 ThatsHandy.cs:34 / Spark.cs:34）✓
    // ②预览它生成的「垃圾喷射器」（升级后预览「垃圾喷射器+」，写法照 LossAversion.cs:35）✓
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Temporary),
        HoverTipFactory.FromCard<RockItLauncher>(IsUpgraded),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每以此法消耗一张临时牌获得的格挡（5 -> 6）。
        new BlockVar("BlockPer", 5m, ValueProp.Move),
        // 配对编号的存储位（生成喷射器时与它写同一个数；卡面不引用，所以不显示）。
        ModCardVars.Int("PairId", 0),
        // 【2026-10-07 用户 QoL 要求】战斗内预览"即将装填几张卡"（= 本次会打出并消耗的临时牌数）。
        //   写法照 Strengthen.cs:45-48：CalculationBase(0) + CalculationExtra(1) × 乘数函数 = 直接显示张数。
        //   乘数函数是 **static lambda**（无闭包 ✓），真正的计数放在具名静态方法里用**显式 foreach** 做
        //   （本仓库铁律：回调/渲染路径不出现闭包与闭包型 LINQ ✗）。
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("WillLoad").WithMultiplier(static (card, _) => CountTemporaryCards(card)),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public RockItLauncherReload() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 本次会被"打出并消耗"的临时牌列表（= 即将装填进喷射器的张数）。
    /// 卡面预览 <c>{WillLoad:diff()}</c> 与实际结算**共用这一个方法** ⇒ 两边永远不会算出不同的数 ✓
    ///
    /// 判定（本 mod 对「临时牌」的权威定义 = 局内生成、不属于牌组，见 Powers/ThisHandyPower.cs:28-29）：
    ///   * <c>DeckVersion == null</c>（不属于牌组）；
    ///   * 排除**本卡自己**（装填本身就是衍生牌，不排除会自递归 ✗）；
    ///   * 排除已在**结算区 Play** 的牌（重复 AutoPlay 会让这次出牌永远收不了尾 ✗）；
    ///   * 排除已在**消耗堆**的牌。
    /// ⚠️ 用**显式 foreach**（不用 LINQ/闭包）：本方法会在卡面预览渲染路径里被调用 ✗
    /// </summary>
    public static List<CardModel> CollectTemporaryCards(CardModel self)
    {
        var result = new List<CardModel>();
        var state = self.Owner?.PlayerCombatState;
        if (state == null)
            return result;

        foreach (var card in state.AllCards)
        {
            if (card == null)
                continue;

            if (ReferenceEquals(card, self))
                continue;

            if (card.DeckVersion != null)
                continue;

            var pile = card.Pile?.Type;
            if (pile == PileType.Exhaust || pile == PileType.Play)
                continue;

            result.Add(card);
        }

        return result;
    }

    /// <summary>只数张数（卡面预览用）。</summary>
    public static int CountTemporaryCards(CardModel self) => CollectTemporaryCards(self).Count;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1) 取牌 + **先确定数量**（格挡要按它算，见第 2 步）
        //    判定与卡面预览共用 CollectTemporaryCards（见其注释；那段历史坑都写在那里）✓
        var temporary = CollectTemporaryCards(this);

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
