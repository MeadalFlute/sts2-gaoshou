using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Gaoshou.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Relics;

// 神经超频器（稀有）：每当你即将失去临时力量或临时敏捷时，仅失去 1 层，然后失去 1 点生命。
// 分开判定（同时失去两种则扣 2 生命）。仅临时力量/临时敏捷＞3 层时生效。
// 实际扣减逻辑在 GaoshouTemporaryStrengthPower / GaoshouTemporaryDexterityPower 的回合结束中接入。
[RegisterRelic(typeof(GaoshouRelicPool))]
public sealed class Sandevistan : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");
}