// 临时力量的回合开始补发逻辑已迁移到 GaoshouTemporaryStrengthPower.AfterSideTurnStart：
// 原先的全局 SideTurnStartedEvent 订阅在多人时用 PlayerCreatures.FirstOrDefault() 取到主机，
// 造成客机回合开始不补发力量。现由能力自身的 AfterSideTurnStart（per-owner、participants 判定）负责。
// 本文件保留为空壳，避免残留引用；如需恢复全局订阅逻辑请在此实现。