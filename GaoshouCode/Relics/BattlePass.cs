using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Gaoshou.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 战斗通行证（商店）：每场战斗结束后，失去 15 金币，获得一组额外的卡牌奖励（参考转经轮）。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class BattlePass : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner)
            return false;
        if (room == null)
            return false;
        // 普通/精英/BOSS 战斗都触发（转经轮只认 Monster，导致精英=Boss漏掉）。
        if (room.RoomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
            return false;

        Flash();
        // 额外一组卡牌奖励（按当前房间类型生成选项）。
        rewards.Add(new CardReward(CardCreationOptions.ForRoom(player, room.RoomType), 3, player));

        // 失去 15 金币（同步方法内异步结算，fire-and-forget）。
        _ = ApplyGoldLossAsync(player);
        return true;
    }

    private static async System.Threading.Tasks.Task ApplyGoldLossAsync(Player player)
    {
        await PlayerCmd.LoseGold(15, player);
    }
}