using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 【2026-09-30】历史记录 / 百科大全里"角色图渲染过大"的尺寸修正。
///
/// 现象（用户实测）：在**百科大全 / 历史记录查看历史对局**时，我们角色的图像**尺寸过大** ✗。
///
/// 根因（尺寸事实 + 代码定位）：
///   * 这两个界面用的是 **`CharacterModel.IconTexture`** 这个槽位：
///       - 历史记录：`NRunHistoryPlayerIcon.cs:64` `_icon.Texture = characterModel.IconTexture;`
///         （`_icon = GetNode&lt;TextureRect&gt;("%Icon")`，见同文件 L55；赋值在 `LoadRun` 内）
///       - 百科大全：`NBestiaryCharacterFilter.cs:137-138`（`%Image` 与 `%Shadow` 都用 `IconTexture`）、
///         `NBestiary.cs:369-370`（`_iconTexture` / `_iconOutlineTexture`）
///   * **该槽位的规范尺寸是 88×88**：
///       - 创意工坊皮肤 mod（`3787753911`）替换的 `character_icon_silent.png` 实测就是 **88×88** ✓
///       - 原版 `character_icon_ironclad.png` / `_outline.png` 的 ctex 都是 **7796 字节**（同尺寸同量级 ⇒ 88×88）✓
///       - 我们从原版槽位拿来的那张 outline 原本也是 **88×88** ✓
///   * 而**我们给的是 200×200** ✗ —— 顶部栏/其它地方没事，是因为**那些场景显式设了**
///     `expand_mode=1`（IgnoreSize）+ `stretch_mode=5`（KeepAspectCentered）把图缩进容器里 ✓
///     （例如我们自己的 `Gaoshou_top_icon.tscn`、原版 `ironclad_icon.tscn` ✓）；
///     而**历史记录的那个 `%Icon` 没有设这两项、也没有设尺寸** ✗ ⇒ 就按**贴图原始尺寸 200×200** 渲染 ✗
///     （场景里只有父节点 `PlayerIcon` 有 `custom_minimum_size = Vector2(64, 64)` ✓）
///     ⇒ **正是用户说的"忘记设置该项尺寸"** ✓✓。
///
/// 修法：**不动共享贴图**（`IconTexture` 顶部栏也在用 ✗ 缩小会连带改坏顶部栏 ✗），
/// 改为在**每一个用到它的界面**上，把显示该贴图的 `TextureRect` 补上尺寸/缩放设置：
///   `CustomMinimumSize = Size = (88, 88)` + `ExpandMode = IgnoreSize` + `StretchMode = KeepAspectCentered`
/// ⇒ 200×200 被等比缩放进 88×88 ⇒ **与原版 88×88 的观感一致** ✓，且只影响"正在显示我们这张图"的节点 ✓
/// （判据：节点贴图的 `ResourcePath` 含 `Gaoshou_character_icon` ⇒ 别的角色/别的图一律不动 ✓）。
/// </summary>
public sealed class CharacterIconDisplaySizePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_character_icon_display_size";

    public static string Description => "give our 200x200 character icon the slot's 88x88 display size in run history / bestiary";

    public static bool IsCritical => false;

    /// <summary>该槽位的规范显示尺寸（见类注释的实测依据）。</summary>
    private static readonly Vector2 DisplaySize = new(88f, 88f);

    public static ModPatchTarget[] GetTargets() =>
    [
        // 历史记录：%Icon（public 方法 ✓）
        PatchTarget.Method(typeof(NRunHistoryPlayerIcon), "LoadRun"),
        // ⚠️ 游戏 2026-09-30 更新后 `NBestiaryCharacterFilter` 这个类**已从原版移除** ✗
        //    （新 DLL 里 Bestiary 相关只剩 NBestiary / NBestiaryEntry / NBestiaryLayout* / BestiaryEntry 等）
        //    ⇒ 原「角色筛选」入口不复存在，不再挂它 ✓。
        // 百科大全：角色详情（私有方法 ⇒ 用 OptionalMethod 按名定位，找不到也只是这条不生效 ✓）
        PatchTarget.OptionalMethod(typeof(NBestiary), "DisplayCharacterData"),
    ];

    public static void Postfix(Node __instance) => FixSubtree(__instance);

    private static void FixSubtree(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is TextureRect rect && IsOurIcon(rect.Texture))
                Apply(rect);
            FixSubtree(child);
        }
    }

    /// <summary>只认"正在显示我们角色头像"的节点（按资源路径判据）⇒ 绝不误伤别的角色/别的图 ✓。</summary>
    private static bool IsOurIcon(Texture2D? texture)
    {
        var path = texture?.ResourcePath ?? string.Empty;
        return path.Contains("Gaoshou_character_icon");
    }

    private static void Apply(TextureRect rect)
    {
        if (rect.CustomMinimumSize == DisplaySize && rect.ExpandMode == TextureRect.ExpandModeEnum.IgnoreSize)
            return;   // 已经设好了（避免重复写入/抖动）

        rect.CustomMinimumSize = DisplaySize;
        rect.Size = DisplaySize;
        // 这两项就是原版角色 icon 场景用的组合（ironclad_icon.tscn: expand_mode=1 / stretch_mode=5）✓
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }
}
