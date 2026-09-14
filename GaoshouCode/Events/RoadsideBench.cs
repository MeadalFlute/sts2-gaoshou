// 路边的工作台（Overgrowth / ACT1）：给牌组里的一张牌附魔，然后离开。
// 设计意图：ACT1 的「一次性强化点」——三种附魔的可用范围不同（锋利只吃攻击牌等），
// 让玩家在「加攻击」「加格挡」「给高费牌减费」之间做一次取舍，而不是无脑变强。
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gaoshou.Patches;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Gaoshou.Events;

[RegisterActEvent(typeof(Overgrowth))]
public sealed class RoadsideBench : ModEventTemplate
{
    // 三种附魔的强度，与施工规范表格一致（锋利 3 / 伶俐 3 / 沉眠精华 1）。
    private const int SpikesAmount = 3;
    private const int LiningAmount = 3;
    private const int PotionAmount = 1;

    // 立绘走模组自己的资源目录约定（与卡牌/遗物一致）：Gaoshou/images/events/<事件键>.png。
    // 文件名必须是 GaoshouEventSettings 里的事件键（小写下划线，如 roadside_bench.png）：
    // RitsuLib 不做 PascalCase↔snake_case 转换，写成 {GetType().Name} 会找不到图（静默回退、立绘空白）。
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: $"{Entry.ResPath}/images/events/roadside_bench.png");

    // 只在 Overgrowth（CurrentActIndex == 0）出现，且要过模组设置里的开关。
    public override bool IsAllowed(IRunState runState)
        => GaoshouEventSettings.IsModEventEnabled("roadside_bench") && runState.CurrentActIndex == 0;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, AddSpikes, InitialOptionKey("SPIKES"),
            HoverTipFactory.FromEnchantment<Sharp>(SpikesAmount)),
        new EventOption(this, AddLining, InitialOptionKey("LINING"),
            HoverTipFactory.FromEnchantment<Adroit>(LiningAmount)),
        new EventOption(this, SmearPotion, InitialOptionKey("POTION"),
            HoverTipFactory.FromEnchantment<SlumberingEssence>(PotionAmount)),
    ];

    private Task AddSpikes() => SelectOneCardAndEnchant<Sharp>(SpikesAmount);

    private Task AddLining() => SelectOneCardAndEnchant<Adroit>(LiningAmount);

    private Task SmearPotion() => SelectOneCardAndEnchant<SlumberingEssence>(PotionAmount);

    /// <summary>
    /// 选 1 张可附魔的牌并附魔，然后结束事件进入 DONE 页。
    /// </summary>
    private async Task SelectOneCardAndEnchant<TEnchantment>(int amount) where TEnchantment : EnchantmentModel
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1);

        // FromDeckForEnchantment 内部已按 enchantment.CanEnchant 过滤
        // （锋利只允许攻击牌、不可打出/已有其他附魔的牌也会被排除），
        // 牌组里没有可用牌时返回空集合 —— 必须先判空，否则会给 null 调 Enchant 直接崩。
        var picked = (await CardSelectCmd.FromDeckForEnchantment(
            Owner!, ModelDb.Enchantment<TEnchantment>(), amount, prefs)).FirstOrDefault();

        if (picked != null)
        {
            CardCmd.Enchant<TEnchantment>(picked, amount);   // Enchant 不是 async，别 await
            await Cmd.CustomScaledWait(0.3f, 0.5f);
        }

        SetEventFinished(PageDescription("DONE"));
    }
}
