using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 囤积：技能（普通）。耗 1 能量。
// 获得 3 层囤积（回合结束保留）；奇迹（进入手牌的方式非回合开始时抽牌）：获得 3 点格挡。
// 升级后获得"保留"。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Stockpile : ModCardTemplate
{
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    // 本张卡是否由"回合开始时抽牌"进入手牌（奇迹的触发条件）。
    private bool _enteredByTurnStartDraw;

        public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Miracle,
    ];

public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/Stockpile.png");

    // 奇迹就绪（进入手牌的方式非"回合开始时抽牌"）时泛橙光。
    protected override bool ShouldGlowGoldInternal => !_enteredByTurnStartDraw;

    // 悬浮释义：囤积、奇迹（自定义词条，仅释义，不上卡面词条行避免与描述重复）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Hoard),
        HoverTipFactory.FromKeyword(GaoshouKeyword.Miracle),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
    ];

    public Stockpile() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 记录进入手牌的方式：奇迹仅在"非回合开始时抽牌"的情况下触发。
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            _enteredByTurnStartDraw = fromHandDraw;
        return Task.CompletedTask;
    }

    // 回合结束时若本卡仍留在手牌（保留/囤积带至下一轮），重置奇迹判定：
    // 下一轮打出时它已不是"回合开始时抽牌"进入手牌，应可触发奇迹。
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == Owner.Creature.Side && this.Pile?.Type == PileType.Hand)
            _enteredByTurnStartDraw = false;
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 3 层囤积（回合结束时保留最多 3 张牌，层数按实际保留数减少）。
        await PowerCmd.Apply<GaoshouHoardPower>(choiceContext, Owner.Creature, 3m, Owner.Creature, this);

        // 奇迹：非回合开始时抽牌进入手牌才触发 → 获得 3 点格挡。
        if (!_enteredByTurnStartDraw)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得"保留"（直接改实例词条）；奇迹格挡 3 -> 5。
        AddKeyword(CardKeyword.Retain);
        DynamicVars.Block.UpgradeValueBy(2m);   // 3 -> 5
    }
}