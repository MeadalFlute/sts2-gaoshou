using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 垃圾喷射器：攻击（衍生/Token）。耗 1 能量 0 辉星。消耗。
// 效果：造成 7(11) 点伤害 {Times:diff()} 次（Times 默认 0 => 未指定次数时打出 0 次）；
//       将 3 张随机「废品牌」加入你的弃牌堆；
//       令"生成自己的那张「垃圾喷射器-装填」"回到你的手牌；消耗。
//
// 次数与回手牌的传递：生成方是 Cards/RockItLauncherReload.cs（写 Times 与 PairId），
// 本卡按 PairId 在**消耗牌堆**里找回那张装填 —— 与 Cards/Reload.cs:89-106（找 DualSMG）完全同一套写法。
// 「废品牌」池 = 实现 IWasteCard 的卡（Keywords/IWasteCard.cs），生成方式照 Cards/WasteNot.cs:67-81。
[RegisterCard(typeof(TokenCardPool))]
public sealed class RockItLauncher : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    // 卡牌颜色 N（无色）。
    public GaoshouCardColor CardColor => GaoshouCardColor.Colorless;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 悬浮释义：预览"生成自己的那一张"装填。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<RockItLauncherReload>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        // 攻击次数：由「垃圾喷射器-装填」写入（默认 0 => 直接打出时 0 次）。
        ModCardVars.Int("Times", 0),
        // 配对编号：与生成它的那张装填同号，用于精确找回它。
        ModCardVars.Int("PairId", 0),
        // 加入弃牌堆的随机废品牌数量。
        ModCardVars.Int("WasteCards", 3),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public RockItLauncher() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1) 造成 Times 次伤害（Times 默认 0：一次都不打，符合"未指定次数"的约定）。
        var times = (int)DynamicVars.GetRequired<IntVar>("Times").BaseValue;
        if (times > 0)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .WithHitCount(times)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target!)
                .Execute(choiceContext);
        }

        // 2) 将 N 张随机「废品牌」加入**弃牌堆**（池与生成方式照 Cards/WasteNot.cs:67-81）。
        var wastePool = ModelDb.AllCardPools
            .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(c => c is IWasteCard)
            .ToList();
        var count = (int)DynamicVars.GetRequired<IntVar>("WasteCards").BaseValue;
        if (count > 0 && wastePool.Count > 0)
        {
            var generated = CardFactory.GetDistinctForCombat(
                    Owner, wastePool, count, Owner.RunState.Rng.CombatCardGeneration)
                .ToList();
            foreach (var waste in generated)
                await CardPileCmd.AddGeneratedCardToCombat(waste, PileType.Discard, Owner);
        }

        // 3) 让"生成自己的那张装填"回到手牌：按 PairId 在消耗牌堆里精确配对（同 Cards/Reload.cs:95-106）。
        var pairId = (int)DynamicVars.GetRequired<IntVar>("PairId").BaseValue;
        if (pairId <= 0)
            return;   // 不是由装填生成的喷射器：无事发生。

        var reloadId = ModelDb.GetId(typeof(RockItLauncherReload));
        var reload = PileType.Exhaust.GetPile(Owner)?.Cards.FirstOrDefault(c =>
            c.Id == reloadId
            && c.DynamicVars.TryGetValue("PairId", out var source)
            && (int)source.BaseValue == pairId);

        if (reload != null)
            await CardPileCmd.Add(reload, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);   // 7 -> 11
    }
}
