using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 偏斜（交换后）：技能（罕见）。耗 0 能量 2 星辉。
// 本回合结束时，你的格挡不会被移除（给予 1 层残影）。
// 奇迹（非回合开始抽牌进入手牌）：重复打出这张牌 1(2) 次。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Deflection : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private bool _enteredByTurnStartDraw;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Deflection.png");

    // 悬浮释义：残影（格挡保留至下回合）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<BlurPower>(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Miracle,
    ];

    public override int CanonicalStarCost => 2;

    public Deflection() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 记录进入手牌方式（奇迹判定）。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            _enteredByTurnStartDraw = fromHandDraw;
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 本体 1 层；奇迹（非回合开始抽牌进手）额外打出 1(2) 次。
        var repeats = 1 + (_enteredByTurnStartDraw ? 0 : (IsUpgraded ? 2 : 1));

        for (var i = 0; i < repeats; i++)
        {
            // 给予 1 层残影（下回合开始格挡不消失）。
            await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：奇迹重复 1 -> 2 次。
    }
}
