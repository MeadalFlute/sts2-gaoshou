using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Settings;

namespace Gaoshou.Patches;

// 高手模组配置页：一键解锁全部模组内容（卡牌/遗物图鉴 + 角色进阶 10）。
// 解锁动作与游戏作弊台 unlock 命令同源：MarkCardAsSeen / MarkRelicAsSeen / CharacterStats.MaxAscension。
// 注意：不动 MaxMultiplayerAscension（多人进阶为全角色通用，避免误伤新玩家）。
[RegisterSingleton]
public sealed class GaoshouUnlockSettings : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => false;

    public GaoshouUnlockSettings()
    {
        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .WithSortOrder(100)
                .WithTitle(T("高手", "Gaoshou: The Pro"))
                .WithDescription(T("高手模组设置。", "Gaoshou mod settings."))
                .AddSection("unlock", section => section
                    .WithTitle(T("内容解锁", "Content Unlock"))
                    .AddButton(
                        "unlock_all",
                        T("解锁全部模组内容", "Unlock All Mod Content"),
                        T("立即解锁", "Unlock Now"),
                        _ => UnlockAll(),
                        ModSettingsButtonTone.Accent,
                        T("解锁全部卡牌/遗物图鉴与角色进阶 10。\n（不含多人通用进阶等级）",
                          "Reveal all Gaoshou cards and relics, and set the character's ascension to 10.\n(Leaves the shared multiplayer ascension level untouched.)"))),
            pageId: "gaoshou_main");
    }

    private static ModSettingsText T(string zh, string en)
    {
        // 随游戏当前语言出文案：zhs 用中文，其余用英文。
        var isZh = string.Equals(LocManager.Instance.Language, "zhs",
            System.StringComparison.OrdinalIgnoreCase);
        return ModSettingsText.Literal(isZh ? zh : en);
    }

    private static void UnlockAll()
    {
        // 卡牌：百科"已见"解锁（遍历高手牌池的卡牌）。
        var cardIds = ModelDb.AllCards
            .Where(c => c.Id?.Entry?.StartsWith("GAOSHOU_CARD") == true)
            .Select(c => c.Id)
            .ToList();
        foreach (var id in cardIds)
            SaveManager.Instance.Progress.MarkCardAsSeen(id);

        // 遗物。
        var relicIds = ModelDb.AllRelics
            .Where(r => r.Id?.Entry?.StartsWith("GAOSHOU_RELIC") == true)
            .Select(r => r.Id)
            .ToList();
        foreach (var id in relicIds)
            SaveManager.Instance.Progress.MarkRelicAsSeen(id);

        // 角色进阶 10（仅高手角色，不动多人通用进阶）。
        foreach (var character in ModelDb.AllCharacters)
        {
            if (character.Id.Entry.StartsWith("GAOSHOU_CHARACTER", System.StringComparison.OrdinalIgnoreCase))
                SaveManager.Instance.Progress.GetOrCreateCharacterStats(character.Id).MaxAscension = 10;
        }

        SaveManager.Instance.SaveProgressFile();
    }
}