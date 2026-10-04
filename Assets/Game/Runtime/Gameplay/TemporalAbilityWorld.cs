using System;
using System.Collections.Generic;
using Game.Contracts.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>C20 本地物理验证世界；固定 Tick 推进、排序触发事实并限制历史内存，不替代 C21 世界构建器。</summary>
    public sealed class TemporalAbilityWorld
    {
        /// <summary>验证固定频率；正式物理配置在后续阶段版本化。</summary>
        public const int TickRate = 60;
        /// <summary>最多同时跟踪的对象数，超出时拒绝注册。</summary>
        public const int MaxBodies = 64;
        /// <summary>配置允许的最大历史秒数，防止无界分配。</summary>
        public const float MaxHistorySeconds = 10f;
        private readonly SortedDictionary<string, TemporalBody2D> _bodies = new SortedDictionary<string, TemporalBody2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, RewindHistory> _histories = new Dictionary<string, RewindHistory>();
        private readonly SortedDictionary<string, TemporalTrigger2D> _triggers = new SortedDictionary<string, TemporalTrigger2D>(StringComparer.Ordinal);
        private readonly Collider2D[] _overlaps = new Collider2D[64];
        private readonly List<string> _removed = new List<string>();
        private readonly int _rewindTicks;
        private bool _unloaded;
        /// <summary>独立的本地 Physics2D 场景。</summary>
        public Scene Scene { get; }
        /// <summary>已完成的模拟 Tick。</summary>
        public long CurrentTick { get; private set; }
        /// <summary>当前世界共享的回溯时长。</summary>
        public float RewindSeconds => (float)_rewindTicks / TickRate;
        /// <summary>每个对象的历史槽位数，包含起始状态。</summary>
        public int HistoryCapacity => _rewindTicks + 1;
        /// <summary>恢复字段列表；C20 仅支持刚体运动字段，位置始终必选。</summary>
        public RewindStateFields RestoreFields { get; }
        /// <summary>快照数组的有效载荷字节数，不含托管容器及 Unity 物理对象开销。</summary>
        public long HistoryPayloadBytes => (long)_histories.Count * HistoryCapacity * 32;

        /// <summary>创建独立物理世界；不修改项目或全局物理设置。</summary>
        /// <param name="rewindSeconds">共享回溯时长，默认两秒，上限十秒。</param>
        /// <param name="fields">需要恢复的刚体字段，必须包含位置。</param>
        /// <exception cref="ArgumentOutOfRangeException">时间或字段配置非法。</exception>
        public TemporalAbilityWorld(float rewindSeconds = 2f, RewindStateFields fields = RewindStateFields.Motion)
        {
            if (float.IsNaN(rewindSeconds) || rewindSeconds <= 0 || rewindSeconds > MaxHistorySeconds)
                throw new ArgumentOutOfRangeException(nameof(rewindSeconds));
            if ((fields & RewindStateFields.Position) == 0 || (fields & ~RewindStateFields.Motion) != 0)
                throw new ArgumentOutOfRangeException(nameof(fields));
            _rewindTicks = Mathf.CeilToInt(rewindSeconds * TickRate);
            RestoreFields = fields;
            Scene = SceneManager.CreateScene("C20-Physics-" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        }

        /// <summary>把根对象放入验证世界；必须在注册前调用。</summary>
        /// <param name="root">无父节点的对象。</param>
        public void AddRoot(GameObject root)
        {
            EnsureLoaded();
            SceneManager.MoveGameObjectToScene(root, Scene);
        }

        /// <summary>注册独立矩形刚体并采集起始历史；拒绝关节、复合碰撞体、重复 ID 和超额对象。</summary>
        /// <param name="target">已配置稳定 ID 的动态物体。</param>
        /// <exception cref="ArgumentException">对象不满足验证适配器约束。</exception>
        /// <exception cref="InvalidOperationException">超过对象预算。</exception>
        public void Register(TemporalBody2D target)
        {
            EnsureLoaded();
            if (target == null || target.gameObject.scene != Scene || string.IsNullOrWhiteSpace(target.StableId) ||
                _bodies.ContainsKey(target.StableId) || target.Body.bodyType != RigidbodyType2D.Dynamic ||
                target.GetComponentsInChildren<Collider2D>(true).Length != 1 || target.Shape.isTrigger ||
                target.GetComponentsInChildren<Joint2D>(true).Length != 0 ||
                target.transform.parent != null || target.transform.lossyScale.x <= 0 || target.transform.lossyScale.y <= 0)
                throw new ArgumentException("C20 requires a unique, independent dynamic box body without joints.", nameof(target));
            if (_bodies.Count >= MaxBodies) throw new InvalidOperationException("C20 body budget exceeded.");
            _bodies.Add(target.StableId, target);
            if (target.AllowRewind)
            {
                var history = new RewindHistory(HistoryCapacity);
                history.Record(target.CaptureState(CurrentTick));
                _histories.Add(target.StableId, history);
            }
        }

        /// <summary>注册已配置入口；最多六十四个，出口必须位于同一本地场景。</summary>
        /// <param name="trigger">回溯或单向传送入口。</param>
        public void Register(TemporalTrigger2D trigger)
        {
            EnsureLoaded();
            if (trigger == null || trigger.gameObject.scene != Scene || string.IsNullOrWhiteSpace(trigger.StableId) ||
                _triggers.ContainsKey(trigger.StableId) || !trigger.GetComponent<BoxCollider2D>().isTrigger ||
                (trigger.Kind == TemporalTriggerKind.Portal &&
                (trigger.Destination == null || trigger.Destination.gameObject.scene != Scene)))
                throw new ArgumentException("Invalid C20 trigger or destination.", nameof(trigger));
            if (_triggers.Count >= 64) throw new InvalidOperationException("C20 trigger budget exceeded.");
            _triggers.Add(trigger.StableId, trigger);
        }

        /// <summary>执行一 Tick：同步变换、推进本地物理、处理触发、记录历史。</summary>
        public void Step()
        {
            EnsureLoaded();
            RemoveDestroyedBodies();
            Physics2D.SyncTransforms();
            foreach (var target in _bodies.Values)
            {
                if (!target.WaitingForPortalExit) continue;
                bool inside = false;
                foreach (var trigger in _triggers.Values)
                    if (trigger != null && trigger.Kind == TemporalTriggerKind.Portal && trigger.Contains(target))
                        inside = true;
                if (!inside) target.WaitingForPortalExit = false;
            }
            CurrentTick++;
            Scene.GetPhysicsScene2D().Simulate(1f / TickRate);
            foreach (var trigger in _triggers.Values)
            {
                if (trigger == null) continue;
                List<TemporalBody2D> entries = trigger.PendingEntries();
                foreach (var target in entries)
                {
                    if (trigger.Consumed || !target.isActiveAndEnabled ||
                        !_bodies.TryGetValue(target.StableId, out var registered) || registered != target ||
                        target.LastDisplacementTick == CurrentTick) continue;
                    if (trigger.Kind == TemporalTriggerKind.Rewind && target.AllowRewind)
                    {
                        trigger.Consume();
                        bool restored = TryRewind(target);
                        trigger.LastResult = target.StableId + (restored ? ": rewound" : ": blocked (consumed)");
                        target.LastDisplacementTick = CurrentTick;
                    }
                    else if (trigger.Kind == TemporalTriggerKind.Portal && target.AllowTeleport &&
                        !target.WaitingForPortalExit && trigger.Destination != null)
                    {
                        target.TeleportTo(trigger.Destination.position);
                        target.WaitingForPortalExit = true;
                        target.LastDisplacementTick = CurrentTick;
                        trigger.LastResult = target.StableId + ": teleported";
                    }
                }
                entries.Clear();
                Physics2D.SyncTransforms();
            }
            foreach (var target in _bodies.Values)
                if (target.isActiveAndEnabled && _histories.TryGetValue(target.StableId, out var history))
                    history.Record(target.CaptureState(CurrentTick));
        }

        /// <summary>开始卸载本地物理场景并释放历史引用；调用方可等待返回的操作完成。</summary>
        /// <returns>首次卸载的异步操作；重复调用返回空。</returns>
        public AsyncOperation Unload()
        {
            if (_unloaded) return null;
            _unloaded = true;
            _bodies.Clear();
            _histories.Clear();
            _triggers.Clear();
            return Scene.IsValid() && Scene.isLoaded ? SceneManager.UnloadSceneAsync(Scene) : null;
        }

        /// <summary>从两秒前或最早历史向现在寻找可用位置，并恢复对应运动状态。</summary>
        /// <param name="target">本次被选中的唯一目标。</param>
        /// <returns>恢复成功返回真；无历史或全部阻挡返回假。</returns>
        private bool TryRewind(TemporalBody2D target)
        {
            if (!_histories.TryGetValue(target.StableId, out var history) ||
                !history.TryFind(CurrentTick - _rewindTicks, state => IsAvailable(target, state), out var found))
                return false;
            target.RestoreState(found, RestoreFields);
            return true;
        }

        /// <summary>在当前本地世界检查历史矩形落点；排除自身和触发器，结果溢出时保守拒绝。</summary>
        /// <param name="target">正在回溯的对象。</param>
        /// <param name="state">候选历史状态。</param>
        /// <returns>不存在实体碰撞阻挡时为真。</returns>
        private bool IsAvailable(TemporalBody2D target, RewindBodyState state)
        {
            BoxCollider2D shape = target.Shape;
            Vector2 scale = target.transform.lossyScale;
            float angle = (RestoreFields & RewindStateFields.Rotation) != 0 ? state.Rotation : target.Body.rotation;
            Vector2 center = state.Position + (Vector2)(Quaternion.Euler(0, 0, angle) * Vector2.Scale(shape.offset, scale));
            Vector2 size = Vector2.Scale(shape.size, scale);
            size.x = Mathf.Max(0.001f, size.x - 0.002f);
            size.y = Mathf.Max(0.001f, size.y - 0.002f);
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(target.gameObject.layer));
            int count = Scene.GetPhysicsScene2D().OverlapBox(center, size, angle, filter, _overlaps);
            if (count == _overlaps.Length) return false;
            for (int index = 0; index < count; index++)
                if (_overlaps[index] != shape && !Physics2D.GetIgnoreCollision(shape, _overlaps[index])) return false;
            return true;
        }

        /// <summary>移除已销毁目标及其历史，避免无限保留失效对象。</summary>
        private void RemoveDestroyedBodies()
        {
            _removed.Clear();
            foreach (var pair in _bodies) if (pair.Value == null) _removed.Add(pair.Key);
            foreach (string id in _removed) { _bodies.Remove(id); _histories.Remove(id); }
        }

        /// <summary>阻止访问已开始卸载的物理世界。</summary>
        private void EnsureLoaded()
        {
            if (_unloaded) throw new InvalidOperationException("C20 world is unloaded.");
        }
    }
}
