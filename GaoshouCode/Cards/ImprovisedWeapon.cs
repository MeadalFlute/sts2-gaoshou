using MegaCrit.Sts2.Core.Commands;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 临时武器：攻击（普通）。耗 0 能量 1 星辉（升级后伤害 4->8）。
// 造成 4(8) 点伤害。向手牌加入 2 张「废品牌」（无色卡，后续单独创建）。
[RegisterCard(typeof(GaoshouCardPool))]
public sealed class ImprovisedWeapon : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.Purple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/ImprovisedWeapon.png");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
    ];

    public ImprovisedWeapon() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 0 能量 1 星辉。
    public override int CanonicalStarCost => 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 向手牌加入 2 张随机「废品牌」（衍生无色卡）。
        // 写法对齐原版「储君·君权自授(ManifestAuthority)」：
        //   CardFactory.GetDistinctForCombat(Owner, pool.GetUnlockedCards(UnlockState, CardMultiplayerConstraint),
        //                                     count, RunState.Rng.CombatCardGeneration)
        //   + CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner)。
        // 前提：GaoshouDerivedCardPool 已注册为共享牌池（[RegisterSharedCardPool]），
        // 否则 CardModel.Pool 在建卡面节点(NCard)时抛 "is not in any card pool"，
        // 异常杀死战斗主循环，表现为游戏卡死。
        // 废品按属性过滤生成（不按池）。
        var candidates = ModelDb.AllCardPools
                .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
                .Where(c => c is IWasteCard)
                .ToList();
        var generated = CardFactory.GetDistinctForCombat(
                Owner,
                candidates,
                2,
                Owner.RunState.Rng.CombatCardGeneration)
            .ToList();
        foreach (var c in generated)
            await CardPileCmd.AddGeneratedCardToCombat(c, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);   // 4 -> 8
    }
}
