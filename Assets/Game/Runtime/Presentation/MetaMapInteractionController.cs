using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>C10 地图白盒的拖动、滚轮缩放与边界限制控制器。</summary>
    public sealed class MetaMapInteractionController : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IScrollHandler
    {
        /// <summary>节点被点击或外部定位时触发。</summary>
        public event Action<int> NodeSelected;

        [SerializeField] private RectTransform content;
        [SerializeField] private Vector2 contentSize = new Vector2(1400f, 800f);
        [SerializeField] private float minZoom = 0.75f;
        [SerializeField] private float maxZoom = 1.5f;
        [SerializeField] private float wheelZoomStep = 0.1f;
        private RectTransform _viewport;
        private Vector2 _dragStart;
        private Vector2 _positionStart;
        private float _zoom = 1f;

        /// <summary>可供地图节点放置的内容层。</summary>
        public RectTransform Content => content;

        /// <summary>最近一次选中的节点索引。</summary>
        public int SelectedNodeIndex { get; private set; } = -1;

        /// <summary>创建透明白盒视口和可拖动内容层。</summary>
        public void BuildPreview()
        {
            if (_viewport != null) return;
            _viewport = GetComponent<RectTransform>();
            Image image = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            if (content == null)
            {
                var contentObject = new GameObject("MapContent", typeof(RectTransform));
                contentObject.transform.SetParent(transform, false);
                content = contentObject.GetComponent<RectTransform>();
                content.sizeDelta = contentSize;
                content.anchoredPosition = Vector2.zero;
            }
            ClampPosition();
        }

        /// <summary>将地图定位到当前节点。</summary>
        /// <param name="nodeIndex">节点在当前地图中的索引。</param>
        public void FocusNode(int nodeIndex)
        {
            BuildPreview();
            SelectedNodeIndex = nodeIndex;
            NodeSelected?.Invoke(nodeIndex);
            content.anchoredPosition = new Vector2(-nodeIndex * 120f * _zoom,
                content.anchoredPosition.y);
            ClampPosition();
        }

        /// <inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            BuildPreview();
            _dragStart = eventData.position;
            _positionStart = content.anchoredPosition;
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (content == null) return;
            content.anchoredPosition = _positionStart + eventData.position - _dragStart;
            ClampPosition();
        }

        /// <inheritdoc/>
        public void OnScroll(PointerEventData eventData)
        {
            BuildPreview();
            _zoom = Mathf.Clamp(_zoom + eventData.scrollDelta.y * wheelZoomStep / 10f,
                minZoom, maxZoom);
            content.localScale = Vector3.one * _zoom;
            ClampPosition();
        }

        /// <summary>限制地图内容不越过视口边界。</summary>
        private void ClampPosition()
        {
            if (_viewport == null || content == null) return;
            float halfWidth = Mathf.Max(0f, content.rect.width * _zoom - _viewport.rect.width) * 0.5f;
            float halfHeight = Mathf.Max(0f, content.rect.height * _zoom - _viewport.rect.height) * 0.5f;
            content.anchoredPosition = new Vector2(
                Mathf.Clamp(content.anchoredPosition.x, -halfWidth, halfWidth),
                Mathf.Clamp(content.anchoredPosition.y, -halfHeight, halfHeight));
        }
    }
}
