using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Characters;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 高手护符：初始遗物。每回合最多 3 次，消耗能量获得 1 星辉，消耗星辉获得 1 能量。
// 右下角显示本回合剩余可用次数（0~3）。
// 必须保留池注册才能被正确加载为初始遗物；若因此进入奖励池（RitsuLib 未自动排除起始遗物），
// 再通过奖励过滤排除——先恢复加载，以实测为准。
[RegisterRelic(typeof(GaoshouRelicPool))]
[RegisterCharacterStarterRelic(typeof(GaoshouCharacter))]
[RegisterTouchOfOrobasRefinement(typeof(Wheelchair))]
public sealed class GaoshouAmulet : ModRelicTemplate, IRelicExtraIconAmountLabelsProvider, IRelicExtraIconAmountLabelsChangeSource
{
    private const int UsesPerTurn = 3;
    private int _usesRemaining;

    public event Action? RelicExtraIconAmountLabelsInvalidated;

    public override RelicRarity Rarity => RelicRarity.Starter;

    // 供风暴泛光等读取剩余转换次数。
    public int RemainingUses => _usesRemaining;

    public bool HasUsesRemainingForBoth(int energyCost, int starsCost)
        => _usesRemaining >= System.Math.Max(energyCost, starsCost);

    // 描述中的能量/星辉图标变量。
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Energy("Energy", 1),
        ModCardVars.Stars("Star", 1),
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 右下角显示剩余可用次数 0~3。
    public IReadOnlyList<ExtraIconAmountLabelSlot> GetRelicExtraIconAmountLabelSlots()
        => [ExtraIconAmountLabelSlot.At(ExtraIconAmountLabelCorner.BottomRight, _usesRemaining.ToString())];

    private void NotifyCounterChanged()
    {
        RelicExtraIconAmountLabelsInvalidated?.Invoke();
    }

    // 每回合开始时重置可用次数。
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        _usesRemaining = UsesPerTurn;
        NotifyCounterChanged();
        return Task.CompletedTask;
    }

    // 消耗能量 → 获得 1 星辉。仅对装备者自己的能量消耗生效（多人防串触发）。
    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (amount <= 0 || _usesRemaining <= 0)
            return;
        // 消耗这张牌的人 = 卡牌 Owner（AfterEnergySpent 无 spender 参数，用卡主判定）。
        if (card.Owner != Owner)
            return;

        await PlayerCmd.GainStars(1, Owner);
        _usesRemaining--;
        NotifyCounterChanged();
    }

    // 消耗星辉 → 获得 1 能量。仅对装备者自己的星辉消耗生效（多人防串触发）。
    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (amount <= 0 || _usesRemaining <= 0)
            return;
        if (spender != Owner)
            return;

        await PlayerCmd.GainEnergy(1, spender);
        _usesRemaining--;
        NotifyCounterChanged();
    }
}
