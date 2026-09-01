中文 | [EN](README-EN.md)

# 高手（Gaoshou） — Slay the Spire 2 角色模组

> **开发中 Warning**：本模组仍处于**开发阶段**，很可能存在 BUG，且**卡牌平衡性有待调整**。欢迎反馈与建议。

## 项目简介

「高手」是一个为 **[Slay the Spire 2](https://store.steampowered.com/app/2868840/Slay_the_Spire_2/)** 制作的角色模组，基于 **RitsuLib 0.5.x** 框架开发（C# / Harmony / Godot 资源管线）。模组提供：

- 全新角色「高手」及其专属卡池、遗物、能力
- 双资源体系：**能量** 与 **星辉** 双费用玩法
- 特色关键词：**流转**（与上一张牌颜色完全不同时触发）、**风暴**（满足资源门槛时重复打出）、**增幅**、**奇迹**（非回合开始抽牌进入手牌时触发）、**囤积** 等
- 牌面卡图基于原版素材改色渲染（含双色渐变/径向主题）

## 玩法速览

- 战斗中同时消耗 **能量** 与 **星辉** 出牌；部分卡牌以星辉为费用，需规划资源分配。
- 卡牌颜色决定 **流转** 链与 **风暴** 判定：合理配牌形成连打循环。
- **奇迹**：经由生成、保留等非抽牌途径进入手牌的卡牌，打出时额外触发奇迹效果。
- 派系向卡组：红色输出 / 蓝色控制 / 紫色成长 / 绿色生存，可自由混搭。

## 目录结构

```
GaoshouCode/   C# 源码（卡牌/遗物/能力/补丁）
Gaoshou/       资源根（localization 多语言、images、scenes）
```

## 鸣谢

- **素材来源**：卡面/图标素材提取自游戏 **[骰子浪游者（Diceomancer）](https://store.steampowered.com/app/2501600/_/)**，**素材版权归原作者所有**；本项目仅作学习用途的角色化再创作。
- **AssetRipper**：用于提取素材资源 — https://github.com/AssetRipper/AssetRipper
- **RitsuLib**：模组框架 — https://github.com/Ritsu-Ritsu/RitsuLib
- **LexNinja2**：模组框架与牌池注入等实现参考 — https://github.com/Flimsyyy/LexNinja2
- **计划妥当多人联机修复（Well Laid Plans Multiplayer）**：多人补丁实现参考 — https://github.com/Redem714233/WellLaidPlansMultiplayer

## 反馈

- 🐛 遇到 BUG？请按 [BUG 报告模板](.github/ISSUE_TEMPLATE/bug_report.md) 提交
- 💡 有想法？请按 [功能建议模板](.github/ISSUE_TEMPLATE/feature_request.md) 提交
- ⚖️ 平衡性意见，欢迎一并附在建议中