using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Characters;

public sealed class GaoshouPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "Gaoshou";

    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
    public override Color LabOutlineColor => GaoshouCharacter.ThemeColor;

    // 即使当前版本暂无示例药水，也先把角色药水池结构留好。
}
