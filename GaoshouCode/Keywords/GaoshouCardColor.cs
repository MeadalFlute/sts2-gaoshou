namespace Gaoshou.Keywords;

// 卡牌颜色（对应 DICEOMANCER 的 ColorE：红/蓝/紫/绿/无色/黑，及双色）。
// 红色能量 == 能量，蓝色能量 == 星辉（资源：见卡的 energyCost/CanonicalStarCost）。
// 颜色是卡牌身份标识，用于流转(Flow)配色链与卡面色调；流转机制暂未实装，先仅作元数据。
public enum GaoshouCardColor
{
    Red,            // R  红（能量）
    Blue,           // B  蓝（星辉）
    Purple,         // P  紫
    Green,          // G  绿
    Colorless,      // N  无色
    Black,          // K  黑
    RedBlue,        // RB
    RedPurple,      // RP
    BluePurple,     // BP
    RedGreen,       // RG
    BlueGreen,      // BG
    ColorlessPurple,// NP
    ColorlessBlack, // NB
}
