using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 重整思路：技能（罕见）。耗 0 能量 0 星辉。
// 弃掉所有手牌，抽 4 张牌；奇迹（默认直接触发）：获得 1 能量、1 星辉。消耗（升级后移除）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Reconsider : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Reconsider.png");

    // 奇迹就绪（进入手牌的方式非"回合开始时抽牌"）时泛橙光。
    protected override bool ShouldGlowGoldInternal => !_enteredByTurnStartDraw;

    // 本张卡是否由"回合开始时抽牌"进入手牌（奇迹的触发条件）。
    private bool _enteredByTurnStartDraw;

    // 悬浮释义：奇迹（自定义词条）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

    // 消耗；升级后移除。
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Miracle,
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("EnergyGain", 1),
        ModCardVars.Stars("StarGain", 1),
    ];

    public Reconsider() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 能量 0 星辉：不覆写 CanonicalStarCost。
    }

    // 记录进入手牌的方式：奇迹仅在"非回合开始时抽牌"的情况下触发。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            _enteredByTurnStartDraw = fromHandDraw;
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 弃掉所有手牌。
        await CardCmd.Discard(choiceContext, Owner.PlayerCombatState!.Hand.Cards);

        // 抽 4 张牌。
        await CardPileCmd.Draw(choiceContext, 4, Owner);

        // 奇迹：非回合开始时抽牌进入手牌才触发 → 获得 1 能量、1 星辉。
        if (!_enteredByTurnStartDraw)
        {
            await PlayerCmd.GainEnergy(DynamicVars.GetRequired<EnergyVar>("EnergyGain").BaseValue, Owner);
            await PlayerCmd.GainStars(DynamicVars.GetRequired<StarsVar>("StarGain").BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后移除"消耗"（直接改实例词条）。
        RemoveKeyword(CardKeyword.Exhaust);
    }
}