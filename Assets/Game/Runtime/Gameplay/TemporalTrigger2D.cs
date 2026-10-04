using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>位移能力类别。</summary>
    public enum TemporalTriggerKind
    {
        /// <summary>一次性回溯，失败也消耗。</summary>
        Rewind,
        /// <summary>可复用的单向传送。</summary>
        Portal
    }

    /// <summary>框和场景装置共用的触发组件；物理回调仅排队，由世界在物理后阶段消费。</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TemporalTrigger2D : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private TemporalTriggerKind kind;
        [SerializeField] private Transform destination;
        private readonly List<TemporalBody2D> _pending = new List<TemporalBody2D>();
        /// <summary>稳定排序标识。</summary>
        public string StableId => stableId;
        /// <summary>触发类型。</summary>
        public TemporalTriggerKind Kind => kind;
        /// <summary>单向传送出口；回溯时忽略。</summary>
        public Transform Destination => destination;
        /// <summary>回溯框是否已消耗。</summary>
        public bool Consumed { get; private set; }
        /// <summary>最近一次请求结果，供验证界面展示。</summary>
        public string LastResult { get; internal set; } = "Waiting";

        /// <summary>配置触发器；传送入口必须指定出口。</summary>
        /// <param name="id">世界内唯一稳定标识。</param>
        /// <param name="triggerKind">能力类别。</param>
        /// <param name="exit">传送出口；回溯时可空。</param>
        public void Configure(string id, TemporalTriggerKind triggerKind, Transform exit = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Stable ID is required.", nameof(id));
            if (triggerKind == TemporalTriggerKind.Portal && exit == null)
                throw new ArgumentNullException(nameof(exit));
            stableId = id;
            kind = triggerKind;
            destination = exit;
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        /// <summary>只记录首次进入事件，不在物理回调中移动刚体。</summary>
        /// <param name="other">进入的碰撞体。</param>
        private void OnTriggerEnter2D(Collider2D other)
        {
            var target = other.attachedRigidbody == null ? null : other.attachedRigidbody.GetComponent<TemporalBody2D>();
            if (!Consumed && target != null && !_pending.Contains(target)) _pending.Add(target);
        }

        /// <summary>按稳定对象 ID 排序当前 Tick 的首次进入事件。</summary>
        /// <returns>本组件拥有的暂存列表；调用方处理后必须清空。</returns>
        internal List<TemporalBody2D> PendingEntries()
        {
            _pending.RemoveAll(target => target == null);
            _pending.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            return _pending;
        }

        /// <summary>检查目标当前是否仍与入口重叠，用于传送后重新武装。</summary>
        /// <param name="target">待检查对象。</param>
        /// <returns>入口有效且与目标重叠时为真。</returns>
        internal bool Contains(TemporalBody2D target) => isActiveAndEnabled &&
            GetComponent<BoxCollider2D>().Distance(target.Shape).isOverlapped;

        /// <summary>立即停用一次性回溯框，不论请求是否成功。</summary>
        internal void Consume()
        {
            Consumed = true;
            gameObject.SetActive(false);
        }
    }
}
