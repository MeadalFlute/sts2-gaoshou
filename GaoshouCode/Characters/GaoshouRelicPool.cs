using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Characters;

public sealed class GaoshouRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "Gaoshou";
    public override Color LabOutlineColor => GaoshouCharacter.ThemeColor;

    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
}
