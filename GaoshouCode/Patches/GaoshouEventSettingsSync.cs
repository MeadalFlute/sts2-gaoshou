using System;
using System.Text.Json;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Networking.Sidecar;

namespace Gaoshou.Patches;

/// <summary>
/// 「事件设置」的**主机权威 + 广播给客机**联机同步（RitsuLib Sidecar 配置主题）。
///
/// ## 为什么必须同步
/// RitsuLib 的模组设置是**每个客户端各存各的**（<see cref="STS2RitsuLib.Utils.Persistence.SaveScope.Global" />，
/// 写在各人的 settings.json 里），不参与联机同步。但设置会影响**两端各自算出来的东西**：
///   * <see cref="VanillaEventDisablePatch" /> 会改动本章事件池（<c>RoomSet.events</c> 写进 run 存档，
///     而 <c>RoomSet.NextEvent</c> 走 <c>eventsVisited % events.Count</c>）；
///   * 本模组事件走各事件自己的 <c>IsAllowed</c>（读 <see cref="GaoshouEventSettings.IsModEventEnabled" />），
///     同样影响事件池生成。
/// 事件是在两端各自 <c>ActModel.PullNextEvent</c> 里抽的，设置不同 ⇒ 事件池不同 ⇒ 两端进不同事件房、
/// <c>VisitedEventIds</c> 永久分歧，之后每次抽取都继续分歧（故障会放大，不是只错一个房间）。
///
/// ## 做法：RitsuLib 自带的「房主权威配置同步」
///   * 注册主题 <see cref="Topic" />（<c>RegisterTopic&lt;State, State&gt;</c>），**客机不可改**
///     （<c>canClientRequest =&gt; false</c>）；
///   * 房主在「新一局 / 读档 / 进新章节 / 每次抽事件前」调用 <c>PublishHostState</c> 广播当前快照；
///   * 客机收到快照后覆盖本地主题状态（<c>TopicChanged</c>），并且**读取设置时一律用房主快照**
///     （<see cref="Resolve" />）；房主改完设置后下一次抽事件前也会广播；
///   * ⚠️ RitsuLib 没有「只改主题本地状态」的公开 API：<c>PublishHostState</c> 广播的是**主题里缓存的那份状态**，
///     所以要让主题里的快照变成新值，**只能重新 RegisterTopic**（见 <see cref="RegisterTopicState" />）。
///     推论：只有房主 / 单人局才允许重新注册；客机收到快照后绝不能再注册，否则会把房主的值覆盖回本地值。
///
/// ## 一致性兜底（拿不到同步状态时）
/// 客机在**收到房主快照之前**猜不出房主的设置，所以 <see cref="TryAlignEventPoolFilter" /> 返回 false
/// （本次不动事件池 = 加同步之前「多人局不筛」的旧行为）。房主侧同理：只有确认**本局所有其它玩家都收得到
/// Sidecar 消息**时才敢筛，否则宁可不筛 —— 两端都不筛，仍然一致。该判定在**一局之内固定**
/// （见 <see cref="_decisionRun" />）：中途 true/false 翻转会在两端制造新的分歧窗口。
///
/// ## 已知残余风险（需要实测）
/// 广播是异步的：房主在第 N 次抽取前广播、客机第 N 次抽取时还没收到（网络晚到）时，理论上仍可能分歧一瞬。
/// 因此本类把广播点铺得尽量早（开局 / 进章节 / 每次抽取前），并且让「要不要筛」在一局内固定。
/// </summary>
internal static class GaoshouEventSettingsSync
{
    /// <summary>同步主题名（全局唯一即可，带上模组名避免和别的模组撞号）。</summary>
    public const string Topic = "gaoshou.event_settings";

    /// <summary><c>TopicChanged</c> 可能来自 Sidecar 接收线程，快照字段统一用这把锁保护。</summary>
    private static readonly object Gate = new();

    private static bool _installed;
    private static bool _unavailable;

    /// <summary>正在广播我们自己的快照（此时收到的 <c>TopicChanged</c> 不是「房主发来的」）。</summary>
    private static bool _publishing;

    private static bool _warnedNoSnapshot;
    private static bool _warnedClientWrite;
    private static bool _warnedHostNotFiltering;

    /// <summary>房主广播过来的设置（客机读它；还没收到过就是 null）。</summary>
    private static GaoshouEventSettingsData? _hostSettings;

    /// <summary>房主快照里「房主本局是否真的在筛原版事件池」。</summary>
    private static bool _hostFiltersVanillaPool;

    /// <summary>房主「本局要不要筛」的判定属于哪一局（一局之内固定）。</summary>
    private static IRunState? _decisionRun;
    private static bool _decisionValue;

    // ---------------- 对外（GaoshouEventSettings 调用） ----------------

    /// <summary>
    /// 注册同步主题 + 订阅开局/读档/进章节/结束的生命周期事件（<see cref="GaoshouEventSettings" /> 初始化时调一次）。
    /// 注册失败（Sidecar 不可用 / 版本不符）时静默降级：各端继续用本地设置，和加同步之前一样。
    /// </summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed || _unavailable)
                return;
        }

        try
        {
            RegisterTopicState(GaoshouEventSettings.LocalForSync(), hostFiltersVanillaPool: true);
            RitsuLibSidecarConfigSyncService.TopicChanged += OnTopicChanged;
            lock (Gate)
                _installed = true;

            Entry.Logger.Info($"[EventSync] topic '{Topic}' registered (event settings are host-authoritative).");
        }
        catch (Exception ex)
        {
            lock (Gate)
                _unavailable = true;

            Entry.Logger.Warn($"[EventSync] topic '{Topic}' unavailable; event settings stay local-only: {ex.Message}");
            return;
        }

        // 提前广播点：失败也只是「广播晚一点」（每次抽事件前仍会广播），不影响正确性。
        try
        {
            RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(e => OnRunBoundary(e.RunState));
            RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(e => OnRunBoundary(e.RunState));
            RitsuLibFramework.SubscribeLifecycle<ActEnteredEvent>(e => OnActEntered(e.RunState));
            RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(_ => ClearRunState());
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[EventSync] lifecycle publish points unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// 读取设置时用哪一份：**联机客机用主机快照**；单人局 / 房主端用本地设置
    /// （后两种情况下与加联机同步之前的行为完全一致）。
    /// </summary>
    public static GaoshouEventSettingsData Resolve()
    {
        if (CurrentRole() == NetGameType.Client)
        {
            lock (Gate)
            {
                if (_hostSettings != null)
                    return _hostSettings;
            }

            // 还没收到房主快照：退回本地值（事件池那条路会用 TryAlignEventPoolFilter 拦住，不会因此分歧）。
        }

        return GaoshouEventSettings.LocalForSync();
    }

    /// <summary>
    /// 本地设置写入后调用：房主把新快照广播出去；客机忽略本地改动（联机里事件设置以房主为准）。
    /// </summary>
    public static void OnLocalSettingsChanged()
    {
        if (!_installed)
            return;

        if (CurrentRole() == NetGameType.Client)
        {
            if (!_warnedClientWrite)
            {
                _warnedClientWrite = true;
                Entry.Logger.Info("[EventSync] local change ignored: in multiplayer the host owns the event settings.");
            }

            return;
        }

        bool hasDecision;
        bool filterActive;
        lock (Gate)
        {
            hasDecision = _decisionRun != null;
            filterActive = _decisionValue;
        }

        // 还没为某一局判定过（多半在主菜单）：不广播。开局 / 抽取前那次广播会带上完整快照。
        if (hasDecision)
            PublishHostSnapshot(filterActive, "local change");
    }

    /// <summary>
    /// 抽取事件池前对齐「本次要不要按设置剔除原版事件」（<see cref="VanillaEventDisablePatch" /> 调用）。
    /// 返回 false ⇒ 调用方**不要**动事件池。
    ///   ① 单人局：本地设置就是唯一权威 —— 与加同步之前完全一致；
    ///   ② 联机房主：先按「现有设置 + 本局是否筛」广播一次，再照自己的设置筛；
    ///   ③ 联机客机：照房主快照行事（还没收到快照 ⇒ false）；
    ///   ④ 其它（多人局但网络服务不可用）：false。
    /// </summary>
    public static bool TryAlignEventPoolFilter(RunState runState)
    {
        var playerCount = runState.Players.Count;

        if (playerCount <= 1)
            return true;

        // 主题没注册成功（Sidecar 不可用 / 版本不符）：客机永远收不到快照，房主也不能筛，否则必然分歧。
        if (!IsInstalled)
        {
            if (!_warnedNoSnapshot)
            {
                _warnedNoSnapshot = true;
                Entry.Logger.Warn("[EventSync] sync topic unavailable; leaving the vanilla event pool untouched in multiplayer.");
            }

            return false;
        }

        switch (CurrentRole())
        {
            case NetGameType.Host:
            {
                var filterActive = HostFilterDecision(runState);
                PublishHostSnapshot(filterActive, "event pull");
                return filterActive;
            }
            case NetGameType.Client:
            {
                bool received;
                bool filterActive;
                lock (Gate)
                {
                    received = _hostSettings != null;
                    filterActive = _hostFiltersVanillaPool;
                }

                if (!received)
                {
                    // 猜不出房主的设置：本次不动事件池（两端保守地都不筛 = 加同步之前的行为）。
                    if (!_warnedNoSnapshot)
                    {
                        _warnedNoSnapshot = true;
                        Entry.Logger.Warn(
                            "[EventSync] no host snapshot yet; leaving the vanilla event pool untouched this pull.");
                    }

                    return false;
                }

                if (!filterActive && !_warnedHostNotFiltering)
                {
                    _warnedHostNotFiltering = true;
                    Entry.Logger.Info("[EventSync] host is not filtering vanilla events this run; following the host.");
                }

                return filterActive;
            }
            default:
                if (!_warnedNoSnapshot)
                {
                    _warnedNoSnapshot = true;
                    Entry.Logger.Warn(
                        "[EventSync] multiplayer but no net service; leaving the vanilla event pool untouched this pull.");
                }

                return false;
        }
    }

    // ---------------- 内部：广播 / 接收 ----------------

    /// <summary>把主题状态注册成当前快照（房主广播前必须调，见类注释）。</summary>
    private static void RegisterTopicState(GaoshouEventSettingsData settings, bool hostFiltersVanillaPool)
    {
        // RegisterTopic 会**同步**把 initialState 序列化成字符串存进主题，所以直接传当前的设置对象是安全的。
        RitsuLibSidecarConfigSyncService.RegisterTopic<GaoshouEventSettingsSyncState, GaoshouEventSettingsSyncState>(
            Topic,
            new GaoshouEventSettingsSyncState
            {
                Settings = settings,
                HostFiltersVanillaPool = hostFiltersVanillaPool,
            },
            static (_, _) => false,          // 客机不允许改：事件设置以房主为准
            static (_, delta) => delta);     // 占位：上面的策略恒为拒绝，房主不会通过客机请求改状态
    }

    /// <summary>房主：把当前设置 + 「本局是否筛」广播给所有客机（非房主 / 单人局直接跳过）。</summary>
    private static void PublishHostSnapshot(bool hostFiltersVanillaPool, string reason)
    {
        if (!_installed)
            return;

        var net = NetServiceOrNull();
        if (net == null || net.Type != NetGameType.Host)
            return;   // 单人局 / 客机：没有广播可做（客机的值由房主快照覆盖）

        try
        {
            // 先让主题里的快照 = 现在要广播的值，再广播（PublishHostState 播的是主题里缓存的那份）。
            RegisterTopicState(GaoshouEventSettings.LocalForSync(), hostFiltersVanillaPool);

            lock (Gate)
                _publishing = true;
            try
            {
                RitsuLibSidecarConfigSyncService.PublishHostState(net, Topic, 0UL, reason);
            }
            finally
            {
                lock (Gate)
                    _publishing = false;
            }

            Entry.Logger.Debug($"[EventSync] published '{Topic}' (filter={hostFiltersVanillaPool}, {reason}).");
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[EventSync] publish failed ({reason}): {ex.Message}");
        }
    }

    private static void OnTopicChanged(SidecarConfigTopicChangedEvent e)
    {
        if (!string.Equals(e.Topic, Topic, StringComparison.Ordinal))
            return;

        lock (Gate)
        {
            // 自己刚广播出去的那份，不算「房主发来的快照」（房主读的是本地设置，不需要这份）。
            if (_publishing)
                return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<GaoshouEventSettingsSyncState>(e.StateJson);
            if (state?.Settings == null)
                return;

            lock (Gate)
            {
                _hostSettings = state.Settings;
                _hostFiltersVanillaPool = state.HostFiltersVanillaPool;
            }

            Entry.Logger.Info($"[EventSync] host snapshot applied (rev {e.Revision}, by {e.ChangedByPeer}, " +
                              $"filter={state.HostFiltersVanillaPool}, {e.Reason}).");
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[EventSync] failed to read synced topic: {ex.Message}");
        }
    }

    // ---------------- 内部：生命周期 ----------------

    /// <summary>新一局 / 读档：房主重新判定并广播一次（尽量早于本章第一次事件抽取）。</summary>
    private static void OnRunBoundary(IRunState run)
    {
        lock (Gate)
            _decisionRun = null;   // 新的一局，重新判定

        if (CurrentRole() != NetGameType.Host)
            return;

        PublishHostSnapshot(HostFilterDecision(run), "run started");
    }

    /// <summary>进新章节：房主再广播一次（本章事件池会重新生成、事件也会重新抽）。</summary>
    private static void OnActEntered(IRunState run)
    {
        if (CurrentRole() != NetGameType.Host)
            return;

        PublishHostSnapshot(HostFilterDecision(run), "act entered");
    }

    /// <summary>本局结束：清掉同步状态，别让上一局的房主值影响下一局。</summary>
    private static void ClearRunState()
    {
        lock (Gate)
        {
            _decisionRun = null;
            _decisionValue = false;
            _hostSettings = null;
            _hostFiltersVanillaPool = false;
        }
    }

    // ---------------- 内部：判定 ----------------

    /// <summary>
    /// 房主本局「要不要真的筛原版事件池」：**一局之内固定**（中途翻转会在两端制造新的分歧窗口）。
    /// 只有本局所有其它玩家都能收到 Sidecar 消息时才返回 true。
    /// </summary>
    private static bool HostFilterDecision(IRunState run)
    {
        lock (Gate)
        {
            if (ReferenceEquals(_decisionRun, run))
                return _decisionValue;
        }

        var decision = CanBroadcastToEveryPeer(run.Players.Count);

        lock (Gate)
        {
            _decisionRun = run;
            _decisionValue = decision;
        }

        if (decision)
            Entry.Logger.Info("[EventSync] filtering the vanilla event pool in multiplayer (every peer is reachable).");
        else
            Entry.Logger.Warn($"[EventSync] {run.Players.Count - 1} peer(s) in this run cannot receive Sidecar messages; " +
                              "vanilla-event filtering is disabled for this whole run so that all clients stay consistent.");

        return decision;
    }

    /// <summary>
    /// 本局除自己以外的玩家是否都能收到 Sidecar 消息。判定条件与 RitsuLib 的广播完全一致
    /// （<c>ReadyForBroadcasting</c> + Sidecar 可达）：只要有一个收不到，就不能筛 ——
    /// 客机拿不到快照会算出不同的事件池。
    /// </summary>
    private static bool CanBroadcastToEveryPeer(int playerCount)
    {
        var expected = playerCount - 1;
        if (expected <= 0)
            return true;

        var net = NetServiceOrNull();
        if (net is not NetHostGameService { IsConnected: true } host)
            return false;

        try
        {
            var reachable = 0;
            foreach (var peer in host.ConnectedPeers)
            {
                if (!peer.readyForBroadcasting)
                    continue;

                if (RitsuLibSidecarSessionManager.CanSendToPeer(peer.peerId))
                    reachable++;
            }

            return reachable >= expected;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[EventSync] sidecar reachability check failed, assuming unreachable: {ex.Message}");
            return false;
        }
    }

    private static NetGameType CurrentRole()
    {
        var net = NetServiceOrNull();
        return net?.Type ?? NetGameType.None;
    }

    /// <summary>同步主题是否注册成功（失败 ⇒ 退回「各用本地设置」）。</summary>
    private static bool IsInstalled
    {
        get
        {
            lock (Gate)
                return _installed;
        }
    }

    private static INetGameService? NetServiceOrNull()
    {
        try
        {
            return RunManager.Instance?.NetService;
        }
        catch (Exception ex)
        {
            Entry.Logger.Debug($"[EventSync] net-service lookup failed: {ex.Message}");
            return null;
        }
    }
}

/// <summary>同步主题的状态：房主的一份「事件设置 + 本局是否真的按它筛原版事件池」。</summary>
public sealed class GaoshouEventSettingsSyncState
{
    /// <summary>房主的本模组事件开关 + 被禁用的原版事件（语义与本地设置完全一致）。</summary>
    public GaoshouEventSettingsData Settings { get; set; } = new();

    /// <summary>
    /// 房主本局是否真的在按上面的设置筛原版事件池（房主确认「所有客机都收得到」时才为 true）。
    /// 客机一律照这个值行事，两端才不会分歧；false 时两端都不筛。
    /// </summary>
    public bool HostFiltersVanillaPool { get; set; }
}
