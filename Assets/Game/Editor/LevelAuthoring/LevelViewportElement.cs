using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.Level
{
    /// <summary>
    /// 关卡编辑器 2D 视口; 以数据投影方式绘制关卡内容, 不把 Unity Scene 当作真相来源。
    /// </summary>
    /// <remarks>
    /// 视口只读取 <see cref="LevelAuthoringData"/> 并绘制占位图形。平移、缩放与
    /// 选择结果通过 <see cref="ViewChanged"/> 与 <see cref="SelectionChanged"/> 回传给窗口,
    /// 由窗口决定何时写回 Authoring 数据。视口自身不修改关卡数据。
    /// </remarks>
    public sealed class LevelViewportElement : VisualElement
    {
        /// <summary>世界单位到屏幕像素的缩放步进。</summary>
        public const float ZoomStep = 1.15f;

        /// <summary>允许的最小缩放。</summary>
        public const float MinZoom = 2f;

        /// <summary>允许的最大缩放。</summary>
        public const float MaxZoom = 200f;

        /// <summary>占位图形的默认绘制尺寸; 世界单位。</summary>
        public const float DefaultGlyphSize = 1f;

        private readonly List<StageObjectGlyph> _glyphs = new List<StageObjectGlyph>();
        private LevelAuthoringData _data;
        private float _zoom = 32f;
        private Vector2 _pan;
        private bool _dragging;
        private Vector2 _dragOrigin;
        private Vector2 _panOrigin;
        private string _selectedObjectId = string.Empty;

        /// <summary>创建 2D 视口。</summary>
        public LevelViewportElement()
        {
            focusable = true;
            generateVisualContent += DrawContent;
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        /// <summary>视口中心对应的世界坐标; 随平移变化。</summary>
        public Vector2 ViewCenter => new Vector2(-_pan.x / _zoom, _pan.y / _zoom);

        /// <summary>当前缩放; 单位为像素每世界单位。</summary>
        public float Zoom => _zoom;

        /// <summary>当前选中的对象稳定 ID; 未选中时为空字符串。</summary>
        public string SelectedObjectId => _selectedObjectId;

        /// <summary>视口平移或缩放后触发, 供窗口回写 <see cref="EditorViewStateData"/>。</summary>
        public event Action ViewChanged;

        /// <summary>选中对象变化后触发, 参数为新选中的对象稳定 ID。</summary>
        public event Action<string> SelectionChanged;

        /// <summary>绑定关卡数据并重建绘制缓存。</summary>
        /// <param name="data">关卡源数据; 为 null 时清空视口。</param>
        public void SetData(LevelAuthoringData data)
        {
            _data = data;
            RebuildGlyphs();
            MarkDirtyRepaint();
        }

        /// <summary>应用保存的视口状态; 缩放非法时回退到默认值。</summary>
        /// <param name="state">视口状态; 为 null 时使用默认值。</param>
        public void ApplyViewState(EditorViewStateData state)
        {
            _zoom = state != null && state.Zoom >= MinZoom && state.Zoom <= MaxZoom ? state.Zoom : 32f;
            Vector2 center = state != null ? new Vector2(state.ViewCenterX, state.ViewCenterY) : Vector2.zero;
            _pan = new Vector2(-center.x * _zoom, center.y * _zoom);
            _selectedObjectId = state?.SelectedObjectId ?? string.Empty;
            MarkDirtyRepaint();
        }

        /// <summary>把当前视口与选中状态回写到视口状态对象。</summary>
        /// <param name="state">待更新的视口状态; 为 null 时不执行。</param>
        public void CaptureViewState(EditorViewStateData state)
        {
            if (state == null)
                return;
            Vector2 center = ViewCenter;
            state.ViewCenterX = center.x;
            state.ViewCenterY = center.y;
            state.Zoom = _zoom;
            state.SelectedObjectId = _selectedObjectId;
        }

        /// <summary>按稳定对象 ID 选中元素, 不触发 <see cref="SelectionChanged"/>。</summary>
        /// <param name="objectId">对象稳定 ID; 传空字符串表示清除选中。</param>
        public void SetSelection(string objectId)
        {
            _selectedObjectId = objectId ?? string.Empty;
            MarkDirtyRepaint();
        }

        /// <summary>把世界坐标转换为视口本地坐标; 世界 Y 轴向上, 视口 Y 轴向下。</summary>
        /// <param name="world">世界坐标。</param>
        /// <returns>视口本地坐标。</returns>
        public Vector2 WorldToLocal(Vector2 world) =>
            new Vector2(world.x * _zoom + _pan.x, contentRect.height - (world.y * _zoom + _pan.y));

        /// <summary>把视口本地坐标转换为世界坐标。</summary>
        /// <param name="local">视口本地坐标。</param>
        /// <returns>世界坐标。</returns>
        public Vector2 LocalToWorld(Vector2 local) =>
            new Vector2((local.x - _pan.x) / _zoom, (contentRect.height - local.y - _pan.y) / _zoom);

        /// <summary>把视口定位到关卡世界边界中心。</summary>
        public void FocusOnContent()
        {
            if (_data?.Definition?.WorldBounds is BoundsData bounds)
            {
                Vector2 center = new Vector2((bounds.MinX + bounds.MaxX) * 0.5f, (bounds.MinY + bounds.MaxY) * 0.5f);
                _pan = new Vector2(-center.x * _zoom, center.y * _zoom);
            }
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
        }

        /// <summary>重建绘制缓存; 按稳定 ID 排序保证绘制顺序稳定。</summary>
        private void RebuildGlyphs()
        {
            _glyphs.Clear();
            LevelDefinition definition = _data?.Definition;
            if (definition == null)
                return;
            if (definition.Objects != null)
            {
                foreach (StageObjectData item in definition.Objects)
                {
                    if (item == null)
                        continue;
                    _glyphs.Add(
                        new StageObjectGlyph
                        {
                            ObjectId = item.ObjectId ?? string.Empty,
                            Position = new Vector2(item.PositionX, item.PositionY),
                            Size = new Vector2(
                                Mathf.Abs(item.ScaleX) * DefaultGlyphSize,
                                Mathf.Abs(item.ScaleY) * DefaultGlyphSize
                            ),
                            Rotation = item.RotationZ,
                            IsSelected = string.Equals(item.ObjectId, _selectedObjectId, StringComparison.Ordinal),
                        }
                    );
                }
            }
            if (definition.StartPoint != null)
            {
                _glyphs.Add(
                    new StageObjectGlyph
                    {
                        ObjectId = SpawnPointObjectId,
                        Position = new Vector2(definition.StartPoint.PositionX, definition.StartPoint.PositionY),
                        Size = new Vector2(DefaultGlyphSize, DefaultGlyphSize),
                        Rotation = definition.StartPoint.RotationZ,
                        IsSelected = string.Equals(SpawnPointObjectId, _selectedObjectId, StringComparison.Ordinal),
                    }
                );
            }
            if (definition.GoalPoint != null)
            {
                _glyphs.Add(
                    new StageObjectGlyph
                    {
                        ObjectId = GoalPointObjectId,
                        Position = new Vector2(definition.GoalPoint.PositionX, definition.GoalPoint.PositionY),
                        Size = new Vector2(definition.GoalPoint.Width, definition.GoalPoint.Height),
                        Rotation = 0f,
                        IsSelected = string.Equals(GoalPointObjectId, _selectedObjectId, StringComparison.Ordinal),
                    }
                );
            }
            _glyphs.Sort((left, right) => string.CompareOrdinal(left.ObjectId, right.ObjectId));
        }

        /// <summary>绘制世界边界、区域与对象占位图形。</summary>
        /// <param name="context">UI Toolkit 绘制上下文。</param>
        private void DrawContent(MeshGenerationContext context)
        {
            LevelDefinition definition = _data?.Definition;
            if (definition == null)
                return;
            Painter2D painter = context.painter2D;
            painter.lineWidth = 1f;

            if (definition.WorldBounds != null)
            {
                painter.strokeColor = new Color(0.35f, 0.35f, 0.4f);
                DrawPolygon(
                    painter,
                    new[]
                    {
                        new Vector2(definition.WorldBounds.MinX, definition.WorldBounds.MinY),
                        new Vector2(definition.WorldBounds.MaxX, definition.WorldBounds.MinY),
                        new Vector2(definition.WorldBounds.MaxX, definition.WorldBounds.MaxY),
                        new Vector2(definition.WorldBounds.MinX, definition.WorldBounds.MaxY),
                    },
                    false
                );
            }

            DrawZones(
                painter,
                definition.DeployableZones,
                new Color(0.2f, 0.7f, 0.3f, 0.15f),
                new Color(0.2f, 0.8f, 0.35f)
            );
            DrawZones(
                painter,
                definition.ForbiddenZones,
                new Color(0.85f, 0.25f, 0.25f, 0.15f),
                new Color(0.9f, 0.3f, 0.3f)
            );

            foreach (StageObjectGlyph glyph in _glyphs)
                DrawGlyph(painter, glyph);
        }

        /// <summary>绘制区域集合。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="zones">区域集合; 可为 null。</param>
        /// <param name="fill">填充色。</param>
        /// <param name="stroke">描边色。</param>
        private void DrawZones(Painter2D painter, List<ZoneData> zones, Color fill, Color stroke)
        {
            if (zones == null)
                return;
            foreach (ZoneData zone in zones)
            {
                if (zone?.Vertices == null || zone.Vertices.Count < 3)
                    continue;
                var points = new Vector2[zone.Vertices.Count];
                for (int i = 0; i < zone.Vertices.Count; i++)
                    points[i] = new Vector2(zone.Vertices[i].X, zone.Vertices[i].Y);
                painter.strokeColor = stroke;
                DrawPolygon(painter, points, true, fill);
            }
        }

        /// <summary>绘制单个对象占位图形。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="glyph">占位图形数据。</param>
        private void DrawGlyph(Painter2D painter, StageObjectGlyph glyph)
        {
            float halfWidth = glyph.Size.x * 0.5f;
            float halfHeight = glyph.Size.y * 0.5f;
            var corners = new[]
            {
                new Vector2(-halfWidth, -halfHeight),
                new Vector2(halfWidth, -halfHeight),
                new Vector2(halfWidth, halfHeight),
                new Vector2(-halfWidth, halfHeight),
            };
            float radians = glyph.Rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 rotated = new Vector2(
                    corners[i].x * cos - corners[i].y * sin,
                    corners[i].x * sin + corners[i].y * cos
                );
                corners[i] = glyph.Position + rotated;
            }
            painter.strokeColor = glyph.IsSelected ? new Color(1f, 0.85f, 0.2f) : new Color(0.75f, 0.78f, 0.85f);
            painter.lineWidth = glyph.IsSelected ? 2f : 1f;
            DrawPolygon(painter, corners, true, new Color(0.25f, 0.3f, 0.4f, 0.6f));
            painter.lineWidth = 1f;
        }

        /// <summary>按世界坐标绘制一个多边形路径。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="worldPoints">世界坐标顶点; 至少三个。</param>
        /// <param name="fill">是否填充。</param>
        /// <param name="fillColor">填充色; 仅 <paramref name="fill"/> 为 true 时使用。</param>
        private void DrawPolygon(Painter2D painter, Vector2[] worldPoints, bool fill, Color fillColor = default)
        {
            if (worldPoints == null || worldPoints.Length < 2)
                return;
            painter.BeginPath();
            painter.MoveTo(WorldToLocal(worldPoints[0]));
            for (int i = 1; i < worldPoints.Length; i++)
                painter.LineTo(WorldToLocal(worldPoints[i]));
            painter.ClosePath();
            if (fill)
            {
                painter.fillColor = fillColor;
                painter.Fill();
            }
            painter.Stroke();
        }

        /// <summary>处理滚轮缩放; 以指针位置为锚点。</summary>
        /// <param name="evt">滚轮事件。</param>
        private void OnWheel(WheelEvent evt)
        {
            Vector2 anchor = evt.localMousePosition;
            Vector2 worldBefore = LocalToWorld(anchor);
            float factor = evt.delta.y > 0f ? ZoomStep : 1f / ZoomStep;
            float next = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(next, _zoom))
                return;
            _zoom = next;
            Vector2 worldAfter = LocalToWorld(anchor);
            Vector2 delta = worldAfter - worldBefore;
            _pan += new Vector2(delta.x * _zoom, -delta.y * _zoom);
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
            evt.StopPropagation();
        }

        /// <summary>处理按下: 记录拖拽起点或选中命中的对象。</summary>
        /// <param name="evt">鼠标事件。</param>
        private void OnMouseDown(MouseDownEvent evt)
        {
            Focus();
            if (evt.button == 0)
            {
                string hit = HitTest(LocalToWorld(evt.localMousePosition));
                if (!string.Equals(hit, _selectedObjectId, StringComparison.Ordinal))
                {
                    _selectedObjectId = hit;
                    RebuildGlyphs();
                    MarkDirtyRepaint();
                    ViewChanged?.Invoke();
                    SelectionChanged?.Invoke(hit);
                }
            }
            if (evt.button == 0 || evt.button == 2 || evt.button == 1)
            {
                _dragging = true;
                _dragOrigin = evt.localMousePosition;
                _panOrigin = _pan;
            }
            evt.StopPropagation();
        }

        /// <summary>处理拖拽平移。</summary>
        /// <param name="evt">鼠标事件。</param>
        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_dragging)
                return;
            Vector2 delta = evt.localMousePosition - _dragOrigin;
            _pan = _panOrigin + delta;
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
        }

        /// <summary>处理松开: 结束拖拽。</summary>
        /// <param name="evt">鼠标事件。</param>
        private void OnMouseUp(MouseUpEvent evt)
        {
            _dragging = false;
            evt.StopPropagation();
        }

        /// <summary>处理键盘: Esc 清除选中。</summary>
        /// <param name="evt">键盘事件。</param>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || string.IsNullOrEmpty(_selectedObjectId))
                return;
            _selectedObjectId = string.Empty;
            RebuildGlyphs();
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
            SelectionChanged?.Invoke(string.Empty);
            evt.StopPropagation();
        }

        /// <summary>命中测试; 返回覆盖该世界坐标的对象稳定 ID, 无命中时为空。</summary>
        /// <param name="world">世界坐标。</param>
        /// <returns>对象稳定 ID 或空字符串。</returns>
        private string HitTest(Vector2 world)
        {
            for (int i = _glyphs.Count - 1; i >= 0; i--)
            {
                StageObjectGlyph glyph = _glyphs[i];
                Vector2 half = glyph.Size * 0.5f;
                if (
                    world.x >= glyph.Position.x - half.x
                    && world.x <= glyph.Position.x + half.x
                    && world.y >= glyph.Position.y - half.y
                    && world.y <= glyph.Position.y + half.y
                )
                    return glyph.ObjectId;
            }
            return string.Empty;
        }

        /// <summary>起点在视口中的内部对象 ID; 不进入 Authoring 数据。</summary>
        public const string SpawnPointObjectId = "$spawn";

        /// <summary>终点在视口中的内部对象 ID; 不进入 Authoring 数据。</summary>
        public const string GoalPointObjectId = "$goal";

        /// <summary>绘制缓存中的一个占位图形。</summary>
        private sealed class StageObjectGlyph
        {
            /// <summary>对象稳定 ID。</summary>
            public string ObjectId;

            /// <summary>世界坐标中心点。</summary>
            public Vector2 Position;

            /// <summary>世界单位下的宽高。</summary>
            public Vector2 Size;

            /// <summary>绕 Z 轴旋转角度（度）。</summary>
            public float Rotation;

            /// <summary>是否为当前选中项。</summary>
            public bool IsSelected;
        }
    }
}
