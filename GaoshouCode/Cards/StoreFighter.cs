using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 百货战神：能力（稀有）。耗 2 能量 2 辉星。
// 每回合你（升级后前两次 / 未升级首次）打出【废品】牌：获得 1 层力量、1 层敏捷。
// 【2026-10-05 重做】触发牌张 临时牌→废品牌；收益 临时力敏→**永久力敏各 1**；加"每回合次数上限"刹车。
//   次数上限 = 施加的能力层数（未升级 1 / 升级 2）⇒ 升级即"首次 → 前两次"（因此不再给"固有"）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class StoreFighter : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.BluePurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/StoreFighter.png");

    // 悬浮释义：力量、敏捷（原版能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
    ];

    public StoreFighter() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 2 能量 2 辉星。
    public override int CanonicalStarCost => 2;

    // 能力层数 = 每回合可兑现次数（未升级 1、升级 2）。每次固定给 1 力 1 敏。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Power<DepartmentStoreGodPower>(1m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加能力：每回合前 N 次打出废品牌 → 1 力 1 敏（永久）。
        await PowerCmd.Apply<DepartmentStoreGodPower>(choiceContext, Owner.Creature,
            DynamicVars["DepartmentStoreGodPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级：每回合可兑现次数 1 -> 2（"首次"变"前两次"）。
        DynamicVars["DepartmentStoreGodPower"].UpgradeValueBy(1m);
    }
}