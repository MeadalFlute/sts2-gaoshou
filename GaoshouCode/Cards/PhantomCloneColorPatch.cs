// ⚠️【已撤下：本文件当前是纯注释档，不参与编译，未注册任何补丁】2026-09-22
//
// 这里原本是 PhantomCloneColorPatch：挂在 CombatState.CloneCard 上的补丁，
// 会给**所有**克隆出来的双色卡实例随机登记一个单色（供 mana 颜色释义与「流转」判定使用）。
//
// 【为什么撤下】
// 按设计只有「幻影」复制品才该取单色——那由 PhantomSingleton（Keywords\Phantom.cs）在触发幻影时
// 用同步 RNG 登记，已经足够。其它来源的克隆（DualWield 那类外部复制、遗物、事件、UI 预览）
// **都不该取单色**：例如外部克隆一张「杠式霰弹枪」，复制品就该是原本的红紫双色，而不是随机单色。
// 这个补丁挂在通用克隆入口上，等于把"单色"错误地施加到了所有克隆路径 → 撤下。
//
// 【撤下后的正确行为】
// 只有幻影复制品有 PhantomColorRegistry 记录；其它克隆查不到实例色 →
// GaoshouFlowTracker.GetColor 回退到卡牌类型自带的 CardColor（确定性，且联机两端一致）。
//
// 【若将来真要恢复"所有复制都单色"，启用前必须处理两点】
//   1) 取色只能用同步 RNG（card.Owner.RunState.Rng.CombatCardGeneration）：
//      Random.Shared 是每机独立的本地 RNG，同一张双色卡在主机/客机可能抽到不同单色，
//      而「流转」判定正是读这个实例色 → 两端分歧。
//   2) 该补丁还会**推进同步 RNG 流**：战斗中若有"只在单端运行的 UI 预览克隆"
//      （原版 NUpgradePreview / NEnchantPreview 走 CardScope.CloneCard），
//      单端多推一次就会让之后的随机数序列分歧。恢复前请先确认这条路径不可达。
//
// 【撤下前的实现，留档备查】
//
// using System;
// using System.Reflection;
// using MegaCrit.Sts2.Core.Combat;
// using MegaCrit.Sts2.Core.Models;
// using MegaCrit.Sts2.Core.Random;
// using Gaoshou.Keywords;
// using STS2RitsuLib.Patching.Models;
//
// namespace Gaoshou.Cards;
//
// public class PhantomCloneColorPatch : IPatchMethod
// {
//     public static string PatchId => "gaoshou_phantom_clone_color";
//     public static string Description => "random single color for phantom copies of dual-color cards";
//     public static bool IsCritical => false;
//
//     public static ModPatchTarget[] GetTargets() =>
//     [
//         new(typeof(CombatState), "CloneCard"),
//     ];
//
//     public static void Postfix(CardModel card, ref CardModel __result)
//     {
//         if (__result == null || card?.Id?.Entry?.StartsWith("GAOSHOU_CARD") != true)
//             return;
//         if (card.GetType().GetProperty("CardColor")?.GetValue(card) is not GaoshouCardColor srcColor)
//             return;
//
//         var primaries = PhantomColorRegistry.GetPrimaries(srcColor);
//         if (primaries.Count <= 1 || card.Owner?.RunState?.Rng?.CombatCardGeneration is not { } rng)
//         {
//             PhantomColorRegistry.Assign(__result, srcColor);
//             return;
//         }
//
//         PhantomColorRegistry.Assign(__result, rng.NextItem(primaries));
//     }
// }
