using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 脸接大招：能力（罕见）。耗 0 能量。获得 1(2) 层缓冲；将 3 张眩晕加入你的抽牌堆。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class FaceTank : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.RedBlue;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/FaceTank.png");

    // 悬浮释义：缓冲（游戏原生能力）、眩晕（游戏原生状态牌）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<BufferPower>(),
        HoverTipFactory.FromCard<Dazed>(),
    ];

    public FaceTank() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 1(2) 层缓冲（受到伤害时本回合免疫）。
        await PowerCmd.Apply<BufferPower>(choiceContext, Owner.Creature, IsUpgraded ? 2 : 1, Owner.Creature, this);

        // 将 3 张眩晕加入你的抽牌堆。
        for (var i = 0; i < 3; i++)
        {
            var dazed = Owner.Creature.CombatState?.CreateCard(ModelDb.Card<Dazed>(), Owner);
            if (dazed != null)
                CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(dazed, PileType.Draw, Owner));
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：缓冲 1 -> 2。
    }
}