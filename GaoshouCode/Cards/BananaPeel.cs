using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Gaoshou.Characters;
using Gaoshou.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Cards;

// 妫ｆ瑨鏅ч惃顕嗙窗閹垛偓閼虫枻绱欓弮鐘哄鐞涘秶鏁撻敍澶堚偓鍌濃偓?1閿涘牆宕岀痪褍鎮?0閿涘鍏橀柌?0 閺勭喕绶ｉ妴鍌氼嚠閻╊喗鐖ｉ弬钘夊 3 鐏炲倹妲楁导銈冣偓鍌涚Х閼版ぜ鈧?
[RegisterCard(typeof(TokenCardPool))]
public sealed class BananaPeel : ModCardTemplate, Gaoshou.Keywords.IWasteCard
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Token;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    public GaoshouCardColor CardColor => GaoshouCardColor.ColorlessPurple;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Int("Vulnerable", 3),
    ];

    public BananaPeel() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
        // 0 閺勭喕绶ｉ敍姘瑝鐟曞棗鍟?CanonicalStarCost閵?    
        }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target,
            DynamicVars.GetRequired<IntVar>("Vulnerable").BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);   // 閼充粙鍣?1 -> 0
    }
}
