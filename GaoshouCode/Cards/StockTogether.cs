using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using Gaoshou.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 囤积癖：技能（罕见 / 多人游戏牌）。耗 1 能量 0 辉星（升级后 0 能量）。消耗。
//   将一张「囤积+」加入所有人的手牌；并给所有玩家各 N 层「囤积癖」（N 由 IntVar Stacks 驱动）。
//   囤积癖 n：你的回合开始时，对所有敌人造成 (你的囤积层数 × n) 点伤害（具体释义见能力 tooltip）。
// 多人安全：发牌与挂 buff 都按玩家各自处理，结算由各玩家自己的回合钩子触发（per-owner）；
//   队友枚举与原版 Blade Symphony / Outrage 同款——GetTeammatesOf(含自己) 且只取存活的玩家生物。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class StockTogether : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    // 每名玩家获得的「囤积癖」层数。发牌数量固定为 1 张（写在描述字面量里，此处不建变量）。
    public GaoshouCardColor CardColor => GaoshouCardColor.Blue;

    // 多人游戏牌：单人模式下不会出现在卡牌奖励/商店中。
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    // 卡图：六只仓鼠（五名角色的特征 + 囤积本体）扎堆囤货，沿用囤积卡图的纸底与墨线风格。
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/StockTogether.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    // 悬浮释义：囤积（自定义词条）与囤积癖（本卡挂的能力）。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(GaoshouKeyword.Hoard),
        HoverTipFactory.FromPower<GaoshouStockTogetherPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Stacks", 1),
    ];

    public StockTogether() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 辉星：不覆写 CanonicalStarCost，保持默认“无辉星费用”。
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        // 原版 Largesse / Blade Symphony / Outrage 同款起手动画。
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        decimal stacks = DynamicVars.GetRequired<IntVar>("Stacks").BaseValue;

        // GetTeammatesOf 含自己；只对存活玩家生物生效（阵亡队友与非玩家生物跳过）。
        foreach (Creature teammate in combatState.GetTeammatesOf(Owner.Creature))
        {
            if (!teammate.IsAlive || !teammate.IsPlayer || teammate.Player is not { } player)
                continue;

            // 1) 给该玩家一张「囤积+」（升级版囤积：追加「保留」词条、格挡 3 -> 5）。
            CardModel copy = combatState.CreateCard<Stockpile>(player);
            CardCmd.Upgrade(copy);
            CardPileAddResult added = await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);

            // 别人收到的牌不会自动播放入手动画——给施放者预览一下（原版 Outrage 同款）。
            if (!ReferenceEquals(player, Owner))
                CardCmd.PreviewCardPileAdd(added, 2.2f);

            // 2) 给该玩家挂 N 层「囤积癖」：层数 = 本场战斗全队打出囤积癖的累计次数。
            await PowerCmd.Apply<GaoshouStockTogetherPower>(choiceContext, teammate, stacks,
                Owner.Creature, this);

            // 多人依次到账的节奏（原版 Blade Symphony 同款）。
            await Cmd.Wait(0.1f);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后费用 1 -> 0 能量（辉星本就为 0）。
        EnergyCost.UpgradeBy(-1);
    }
}
