using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 分裂双身：技能（稀有）。耗 1 能量。在你打出下一张牌时，额外结算一次该牌（任意类型）。
// 增幅 2(3)：弃置最多 2(3) 张牌，随后这张牌（能力附身）重放。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class SplinterTwin : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/SplinterTwin.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("AmplifyCount", 2),
    ];

    public SplinterTwin() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 主体：让「下一张打出的牌」额外结算一次。
        await PlayApplyAsync(choiceContext);
        // 增幅：弃置最多 2(3) 张牌，重放本体效果（叠加层数）。
        await this.AmplifyAsync(choiceContext, (int)DynamicVars.GetRequired<IntVar>("AmplifyCount").BaseValue,
            _ => PlayApplyAsync(choiceContext));
    }

    private async Task PlayApplyAsync(PlayerChoiceContext choiceContext)
    {
        await PowerCmd.Apply<SplinterTwinPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.GetRequired<IntVar>("AmplifyCount").UpgradeValueBy(1);   // 2 -> 3
    }
}

// 分裂双身（能力）：下一张由装备者打出的牌额外结算一次（任意卡牌类型）；回合结束时移除。
[RegisterPower]
public sealed class SplinterTwinPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/splintertwin.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/splintertwin.png");

    // 任意类型的牌都重复打出（爆发原版限制技能，此处去掉类型判断）。
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner?.Creature != Owner)
            return playCount;
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await PowerCmd.Decrement(this);
    }

    // 回合结束后清空。
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return;
        await PowerCmd.Remove(this);
    }
}