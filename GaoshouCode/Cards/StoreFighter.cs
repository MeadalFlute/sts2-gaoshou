using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 百货战神：能力（稀有）。耗 0 能量 2 星辉。
// 每当你打出【临时】牌后，获得 1 点力量、1 点敏捷（临时暂未实装，简化为"打出任意牌后"触发）。
// 升级后获得"固有"。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class StoreFighter : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/StoreFighter.png");

    // 悬浮释义：临时力量、临时敏捷（自定义能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<GaoshouTemporaryStrengthPower>(),
        HoverTipFactory.FromPower<GaoshouTemporaryDexterityPower>(),
    ];

    public StoreFighter() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 2 星辉。
    public override int CanonicalStarCost => 2;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力：每当你打出牌后获得 1 点力量、1 点敏捷。
        await PowerCmd.Apply<DepartmentStoreGodPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级后获得"固有"（直接改实例词条）。
        AddKeyword(CardKeyword.Innate);
    }
}