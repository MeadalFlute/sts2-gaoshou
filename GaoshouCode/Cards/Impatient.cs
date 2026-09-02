using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 不耐烦：技能（普通）。耗 1 能量（升级 0）。抽 2 张牌。风暴（仅词条）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class Impatient : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Red;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    // 泛光：风暴条件可触发（扣除本卡费用后）。
    protected override bool ShouldGlowGoldInternal =>
        (Owner?.PlayerCombatState?.Stars ?? 0) >= (IsUpgraded ? 2 : 3);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        GaoshouKeyword.Storm,
    ];

    // 风暴条件图标变量（星辉≥3(4)：三/四个星辉图标）。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Stars("StarA", 1),
        ModCardVars.Stars("StarB", 1),
        ModCardVars.Stars("StarC", 1),
        ModCardVars.Stars("StarD", 1),
    ];

    public Impatient() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 风暴：重放 1。用 OnPlay 内部执行两次代替 BaseReplayCount——
    // 后者依赖 AfterCreated 设置，多人远端克隆不会执行会导致状态分歧。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 2, Owner);
        // 风暴（星星星(星)）：当前星辉>=3(4) 才重复抽 2 张。
        if (Owner.PlayerCombatState!.Stars >= (IsUpgraded ? 2 : 3))
            await CardPileCmd.Draw(choiceContext, 2, Owner);
    }

    protected override void OnUpgrade()
    {
        // 费用保持 1；风暴门槛 3 -> 2（升级更易触发）。
    }
}
