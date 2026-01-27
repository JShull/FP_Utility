namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections.Generic;
    using System;

    /// <summary>
    /// Central tick dispatcher for managing Registered IFPTickable objects to get regular update calls.
    /// Mainly allows a way to control update at a per-group tick rate
    /// </summary>
    public class FP_TickSystem : MonoBehaviour
    {
        private static FP_TickSystem _instance;
        public static FP_TickSystem CCTick => _instance;

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Global Control")]
        [SerializeField] private bool paused = false;

        [Tooltip("Applied to scaled-time dt only (Update/LateUpdate/UserInterval when UseUnscaledTime=false).")]
        [SerializeField] private float globalTimeScale = 1f;

        [Header("Tick Groups")]
        [SerializeField] private List<FPTickGroupConfig> groupConfigs = new();

        private readonly Dictionary<int, FPTickGroupConfig> _configByGroup = new();
        private readonly Dictionary<int, List<IFPTickable>> _tickablesByGroup = new();
        private readonly Dictionary<int, float> _accumulatorByGroup = new();
        private readonly HashSet<int> _dirtyGroups = new();

        public bool Paused { get => paused; set => paused = value; }
        public float GlobalTimeScale { get => globalTimeScale; set => globalTimeScale = Mathf.Max(0f, value); }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            RebuildConfigCache();
        }
        private void RebuildConfigCache()
        {
            _configByGroup.Clear();

            for (int i = 0; i < groupConfigs.Count; i++)
            {
                var cfg = groupConfigs[i];

                if (cfg.Mode == FPTickMode.UserInterval)
                {
                    cfg.Interval = Mathf.Max(0.0001f, cfg.Interval);

                    // Default driver if not set meaningfully
                    if (cfg.IntervalDriver != FPTickMode.Update &&
                        cfg.IntervalDriver != FPTickMode.PhysicsUpdate &&
                        cfg.IntervalDriver != FPTickMode.LateUpdate)
                        {
                            cfg.IntervalDriver = FPTickMode.Update;
                        }
                }

                _configByGroup[cfg.GroupId] = cfg;

                if (!_tickablesByGroup.ContainsKey(cfg.GroupId))
                    _tickablesByGroup[cfg.GroupId] = new List<IFPTickable>();

                if (!_accumulatorByGroup.ContainsKey(cfg.GroupId))
                    _accumulatorByGroup[cfg.GroupId] = 0f;
            }
        }

        #region Unity Method Update Loops
        private void Update()
        {
            if (paused) return;
            DispatchNonIntervalGroups(FPTickMode.Update);
            DispatchIntervalGroupsDrivenBy(FPTickMode.Update);
        }

        private void FixedUpdate()
        {
            if (paused) return;
            DispatchNonIntervalGroups(FPTickMode.PhysicsUpdate);
            DispatchIntervalGroupsDrivenBy(FPTickMode.PhysicsUpdate);
        }

        private void LateUpdate()
        {
            if (paused) return;
            DispatchNonIntervalGroups(FPTickMode.LateUpdate);
            DispatchIntervalGroupsDrivenBy(FPTickMode.LateUpdate);
        }
        #endregion
        private void DispatchNonIntervalGroups(FPTickMode callbackMode)
        {
            foreach (var kvp in _configByGroup)
            {
                int groupId = kvp.Key;
                var cfg = kvp.Value;

                if (!cfg.Enabled) continue;
                if (cfg.Mode != callbackMode) continue;
                if (cfg.Mode == FPTickMode.UserInterval) continue;

                if (!_tickablesByGroup.TryGetValue(groupId, out var list) || list.Count == 0)
                    continue;

                SortIfDirty(groupId, list);
                float dt = GetDtForCallback(cfg, callbackMode);
                Dispatch(list, dt);
            }
        }
        private void DispatchIntervalGroupsDrivenBy(FPTickMode callbackMode)
        {
            foreach (var kvp in _configByGroup)
            {
                int groupId = kvp.Key;
                var cfg = kvp.Value;

                if (!cfg.Enabled) continue;
                if (cfg.Mode != FPTickMode.UserInterval) continue;
                if (cfg.IntervalDriver != callbackMode) continue;

                if (!_tickablesByGroup.TryGetValue(groupId, out var list) || list.Count == 0)
                    continue;

                SortIfDirty(groupId, list);

                float dt = GetDtForCallback(cfg, callbackMode);
                float acc = _accumulatorByGroup[groupId] + dt;

                float interval = Mathf.Max(0.0001f, cfg.Interval);
                while (acc >= interval)
                {
                    Dispatch(list, interval); // dt passed to tickable = interval, by design
                    acc -= interval;
                }

                _accumulatorByGroup[groupId] = acc;
            }
        }

        private float GetDtForCallback(FPTickGroupConfig cfg, FPTickMode callbackMode)
        {
            // FixedUpdate driven groups should use fixed delta time
            if (callbackMode == FPTickMode.PhysicsUpdate)
            {
                // Unity doesn’t always expose fixedUnscaledDeltaTime across versions; keep it simple.
                // If you want, we can conditional-compile an unscaled fixed dt variant.
                return Time.fixedDeltaTime;
            }

            // Update/LateUpdate dt
            return cfg.UseUnscaledTime ? Time.unscaledDeltaTime : (Time.deltaTime * globalTimeScale);
        }

        private void SortIfDirty(int groupId, List<IFPTickable> list)
        {
            if (!_dirtyGroups.Contains(groupId)) return;

            list.Sort((a, b) => b.TickPriority.CompareTo(a.TickPriority));
            _dirtyGroups.Remove(groupId);
        }

        private static void Dispatch(List<IFPTickable> list, float dt)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null) continue;
                t.Tick(dt);
            }
        }

        public void Register(IFPTickable tickable)
        {
            if (tickable == null) return;

            int group = tickable.TickGroup;

            if (!_configByGroup.ContainsKey(group))
            {
                // Default group: Update
                var cfg = new FPTickGroupConfig
                {
                    GroupId = group,
                    Mode = FPTickMode.Update,
                    Interval = 0.1f,
                    UseUnscaledTime = false,
                    Enabled = true,
                    IntervalDriver = FPTickMode.Update
                };

                groupConfigs.Add(cfg);
                RebuildConfigCache();
            }

            if (!_tickablesByGroup.TryGetValue(group, out var list))
            {
                list = new List<IFPTickable>();
                _tickablesByGroup[group] = list;
            }

            if (!list.Contains(tickable))
            {
                list.Add(tickable);
                _dirtyGroups.Add(group);
                tickable.OnTickRegistered();
            }
        }

        public void Unregister(IFPTickable tickable)
        {
            if (tickable == null) return;

            int group = tickable.TickGroup;
            if (_tickablesByGroup.TryGetValue(group, out var list))
            {
                if (list.Remove(tickable))
                    tickable.OnTickUnregistered();
            }
        }

        public void MarkGroupDirty(int groupId)
        {
            _dirtyGroups.Add(groupId);
        }
        public void SetGroupConfig(FPTickGroupConfig cfg)
        {
            if (cfg.Mode == FPTickMode.UserInterval)
            {
                cfg.Interval = Mathf.Max(0.0001f, cfg.Interval);
                if (cfg.IntervalDriver != FPTickMode.Update &&
                    cfg.IntervalDriver != FPTickMode.PhysicsUpdate &&
                    cfg.IntervalDriver != FPTickMode.LateUpdate)
                {
                    cfg.IntervalDriver = FPTickMode.Update;
                }
            }

            _configByGroup[cfg.GroupId] = cfg;

            for (int i = 0; i < groupConfigs.Count; i++)
            {
                if (groupConfigs[i].GroupId == cfg.GroupId)
                {
                    groupConfigs[i] = cfg;
                    return;
                }
            }

            groupConfigs.Add(cfg);
        }
    }
}
