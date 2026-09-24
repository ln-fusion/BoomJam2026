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

        /// <summary>顶点与世界边界角手柄的命中半径; 屏幕像素。</summary>
        public const float HandleHitRadiusPixels = 7f;

        /// <summary>顶点与世界边界角手柄的绘制半边长; 屏幕像素。</summary>
        public const float HandleDrawRadiusPixels = 4f;

        /// <summary>区域边的命中宽度; 屏幕像素。</summary>
        public const float EdgeHitWidthPixels = 6f;

        private readonly List<StageObjectGlyph> _glyphs = new List<StageObjectGlyph>();
        private LevelAuthoringData _data;
        private float _zoom = 32f;
        private Vector2 _pan;
        private DragMode _dragMode;
        private Vector2 _dragOrigin;
        private Vector2 _panOrigin;
        private LevelSelection _selected = LevelSelection.None;
        private LevelSelection _dragTarget = LevelSelection.None;
        private Vector2 _dragAnchorOffset;

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

        /// <summary>当前选中的对象稳定 ID; 未选中或选中的不是对象时为空字符串。</summary>
        /// <remarks>保留本属性用于兼容只关心对象选中的调用方。</remarks>
        public string SelectedObjectId =>
            _selected.Kind == LevelSelectionKind.StageObject ? _selected.ObjectId : string.Empty;

        /// <summary>当前选中项。</summary>
        public LevelSelection Selection => _selected;

        /// <summary>是否允许编辑; 关闭时左键只用于平移与选中。</summary>
        public bool IsEditable { get; set; } = true;

        /// <summary>视口平移或缩放后触发, 供窗口回写 <see cref="EditorViewStateData"/>。</summary>
        public event Action ViewChanged;

        /// <summary>选中项变化后触发, 参数为新选中项。</summary>
        public event Action<LevelSelection> SelectionChanged;

        /// <summary>拖动编辑目标时逐帧触发, 参数为本帧的位置与位移。</summary>
        public event Action<ViewportDragUpdate> ItemDragged;

        /// <summary>请求删除当前选中项时触发。</summary>
        public event Action<LevelSelection> DeleteRequested;

        /// <summary>在区域边上双击请求插入顶点时触发。</summary>
        public event Action<ViewportVertexInsertRequest> VertexInsertRequested;

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
            _selected = state == null ? LevelSelection.None : state.ResolveSelection();
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
            state.CaptureSelection(_selected);
        }

        /// <summary>直接设置选中项, 不触发 <see cref="SelectionChanged"/>。</summary>
        /// <param name="selection">新的选中项; 传 <see cref="LevelSelection.None"/> 表示清除选中。</param>
        public void SetSelection(LevelSelection selection)
        {
            _selected = selection;
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
                    var selection = LevelSelection.StageObject(item.ObjectId);
                    _glyphs.Add(
                        new StageObjectGlyph
                        {
                            ObjectId = item.ObjectId ?? string.Empty,
                            Selection = selection,
                            Position = new Vector2(item.PositionX, item.PositionY),
                            Size = new Vector2(
                                Mathf.Abs(item.ScaleX) * DefaultGlyphSize,
                                Mathf.Abs(item.ScaleY) * DefaultGlyphSize
                            ),
                            Rotation = item.RotationZ,
                            IsSelected = selection == _selected,
                        }
                    );
                }
            }
            if (definition.StartPoint != null)
            {
                var selection = LevelSelection.SpawnPoint();
                _glyphs.Add(
                    new StageObjectGlyph
                    {
                        ObjectId = SpawnPointObjectId,
                        Selection = selection,
                        Position = new Vector2(definition.StartPoint.PositionX, definition.StartPoint.PositionY),
                        Size = new Vector2(DefaultGlyphSize, DefaultGlyphSize),
                        Rotation = definition.StartPoint.RotationZ,
                        IsSelected = selection == _selected,
                    }
                );
            }
            if (definition.GoalPoint != null)
            {
                var selection = LevelSelection.GoalPoint();
                _glyphs.Add(
                    new StageObjectGlyph
                    {
                        ObjectId = GoalPointObjectId,
                        Selection = selection,
                        Position = new Vector2(definition.GoalPoint.PositionX, definition.GoalPoint.PositionY),
                        Size = new Vector2(definition.GoalPoint.Width, definition.GoalPoint.Height),
                        Rotation = 0f,
                        IsSelected = selection == _selected,
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
                ZoneKind.Deployable,
                new Color(0.2f, 0.7f, 0.3f, 0.15f),
                new Color(0.2f, 0.8f, 0.35f)
            );
            DrawZones(
                painter,
                definition.ForbiddenZones,
                ZoneKind.Forbidden,
                new Color(0.85f, 0.25f, 0.25f, 0.15f),
                new Color(0.9f, 0.3f, 0.3f)
            );

            foreach (StageObjectGlyph glyph in _glyphs)
                DrawGlyph(painter, glyph);

            DrawWorldBoundsHandles(painter, definition);
            DrawSelectedZoneHandles(painter, definition);
        }

        /// <summary>绘制区域集合; 选中区域使用高亮描边。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="zones">区域集合; 可为 null。</param>
        /// <param name="kind">区域种类, 用于判断选中。</param>
        /// <param name="fill">填充色。</param>
        /// <param name="stroke">描边色。</param>
        private void DrawZones(Painter2D painter, List<ZoneData> zones, ZoneKind kind, Color fill, Color stroke)
        {
            if (zones == null)
                return;
            for (int index = 0; index < zones.Count; index++)
            {
                ZoneData zone = zones[index];
                if (zone?.Vertices == null || zone.Vertices.Count < 3)
                    continue;
                var points = new Vector2[zone.Vertices.Count];
                var hasNullVertex = false;
                for (int i = 0; i < zone.Vertices.Count; i++)
                {
                    if (zone.Vertices[i] == null)
                        hasNullVertex = true;
                    else
                        points[i] = new Vector2(zone.Vertices[i].X, zone.Vertices[i].Y);
                }
                if (hasNullVertex)
                    continue;
                bool isSelected = _selected.IsZone && _selected.ZoneKind == kind && _selected.ZoneIndex == index;
                painter.strokeColor = isSelected ? new Color(1f, 0.85f, 0.2f) : stroke;
                painter.lineWidth = isSelected ? 2f : 1f;
                DrawPolygon(painter, points, true, fill);
                painter.lineWidth = 1f;
            }
        }

        /// <summary>绘制世界边界的四个角手柄, 供拖动调整边界。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="definition">关卡定义。</param>
        private void DrawWorldBoundsHandles(Painter2D painter, LevelDefinition definition)
        {
            if (!IsEditable || definition.WorldBounds == null)
                return;
            Vector2[] corners = GetBoundsCorners(definition);
            for (int i = 0; i < corners.Length; i++)
            {
                bool isSelected = _selected.Kind == LevelSelectionKind.WorldBoundsCorner && (int)_selected.Corner == i;
                DrawHandle(painter, corners[i], isSelected ? new Color(1f, 0.85f, 0.2f) : new Color(0.6f, 0.62f, 0.7f));
            }
        }

        /// <summary>绘制当前选中区域的顶点手柄。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="definition">关卡定义。</param>
        private void DrawSelectedZoneHandles(Painter2D painter, LevelDefinition definition)
        {
            if (!IsEditable || !_selected.IsZone)
                return;
            ZoneData zone = ZoneAt(definition, _selected.ZoneKind, _selected.ZoneIndex);
            if (zone?.Vertices == null)
                return;
            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                PointData vertex = zone.Vertices[i];
                if (vertex == null)
                    continue;
                bool isSelected = _selected.Kind == LevelSelectionKind.ZoneVertex && _selected.VertexIndex == i;
                DrawHandle(
                    painter,
                    new Vector2(vertex.X, vertex.Y),
                    isSelected ? new Color(1f, 0.85f, 0.2f) : new Color(0.35f, 0.9f, 0.95f)
                );
            }
        }

        /// <summary>绘制一个固定像素尺寸的方形手柄。</summary>
        /// <param name="painter">绘制器。</param>
        /// <param name="world">手柄中心的世界坐标。</param>
        /// <param name="color">手柄颜色。</param>
        private void DrawHandle(Painter2D painter, Vector2 world, Color color)
        {
            Vector2 center = WorldToLocal(world);
            float radius = HandleDrawRadiusPixels;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x - radius, center.y - radius));
            painter.LineTo(new Vector2(center.x + radius, center.y - radius));
            painter.LineTo(new Vector2(center.x + radius, center.y + radius));
            painter.LineTo(new Vector2(center.x - radius, center.y + radius));
            painter.ClosePath();
            painter.fillColor = color;
            painter.Fill();
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

        /// <summary>
        /// 处理按下: 命中可编辑目标时开始拖动, 命中空白时平移视图。
        /// </summary>
        /// <param name="evt">鼠标事件。</param>
        /// <remarks>
        /// 中键与 Alt+左键始终平移, 便于在密集对象上方移动视图。
        /// </remarks>
        private void OnMouseDown(MouseDownEvent evt)
        {
            Focus();
            bool wantsPan = evt.button == 2 || evt.altKey;
            if (evt.button == 0 && !wantsPan)
            {
                // 双击落在区域边上时插入顶点, 不再进入拖动, 避免顺手把区域拖偏。
                if (evt.clickCount == 2 && IsEditable && TryRequestVertexInsert(evt.localMousePosition))
                {
                    evt.StopPropagation();
                    return;
                }
                Vector2 world = LocalToWorld(evt.localMousePosition);
                LevelSelection hit = HitTest(world);
                bool selectionChanged = hit != _selected;
                _selected = hit;
                if (selectionChanged)
                {
                    RebuildGlyphs();
                    MarkDirtyRepaint();
                    ViewChanged?.Invoke();
                    SelectionChanged?.Invoke(hit);
                }
                if (IsEditable && !hit.IsNone && IsDraggable(hit))
                {
                    _dragMode = DragMode.Item;
                    _dragTarget = hit;
                    _dragAnchorOffset = ResolveAnchor(hit) - world;
                    _dragOrigin = evt.localMousePosition;
                    _panOrigin = _pan;
                    evt.StopPropagation();
                    return;
                }
            }
            if (evt.button == 0 || evt.button == 2 || evt.button == 1)
            {
                _dragMode = DragMode.Pan;
                _dragOrigin = evt.localMousePosition;
                _panOrigin = _pan;
            }
            evt.StopPropagation();
        }

        /// <summary>处理拖拽: 平移视图或移动编辑目标。</summary>
        /// <param name="evt">鼠标事件。</param>
        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_dragMode == DragMode.None)
                return;
            if (_dragMode == DragMode.Pan)
            {
                _pan = _panOrigin + (evt.localMousePosition - _dragOrigin);
                MarkDirtyRepaint();
                ViewChanged?.Invoke();
                return;
            }
            Vector2 world = LocalToWorld(evt.localMousePosition) + _dragAnchorOffset;
            Vector2 previous = LocalToWorld(_dragOrigin) + _dragAnchorOffset;
            if (world == previous)
                return;
            _dragOrigin = evt.localMousePosition;
            ItemDragged?.Invoke(new ViewportDragUpdate(_dragTarget, world, world - previous));
        }

        /// <summary>处理松开: 结束拖拽。</summary>
        /// <param name="evt">鼠标事件。</param>
        private void OnMouseUp(MouseUpEvent evt)
        {
            _dragMode = DragMode.None;
            _dragTarget = LevelSelection.None;
            evt.StopPropagation();
        }

        /// <summary>处理键盘: Esc 清除选中, Delete 请求删除选中项。</summary>
        /// <param name="evt">键盘事件。</param>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                if (_selected.IsNone)
                    return;
                _selected = LevelSelection.None;
                RebuildGlyphs();
                MarkDirtyRepaint();
                ViewChanged?.Invoke();
                SelectionChanged?.Invoke(LevelSelection.None);
                evt.StopPropagation();
                return;
            }
            if (IsEditable && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) && !_selected.IsNone)
            {
                DeleteRequested?.Invoke(_selected);
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// 命中测试; 按 顶点手柄 → 世界边界角 → 起点终点 → 对象 → 区域边 → 区域体 的优先级返回目标。
        /// </summary>
        /// <param name="world">世界坐标。</param>
        /// <returns>命中的选中项; 无命中时为 <see cref="LevelSelection.None"/>。</returns>
        /// <remarks>
        /// 对象必须排在区域体之前: 部署区通常覆盖大片区域, 若区域体优先则区域内对象无法点选。
        /// 禁放区排在可部署区之前, 与绘制层级一致（后绘制者在上）。
        /// </remarks>
        private LevelSelection HitTest(Vector2 world)
        {
            LevelDefinition definition = _data?.Definition;
            if (definition == null)
                return LevelSelection.None;

            float handleTolerance = HandleHitRadiusPixels / Mathf.Max(_zoom, 0.0001f);
            LevelSelection vertexHit = HitZoneVertex(definition, world, handleTolerance);
            if (!vertexHit.IsNone)
                return vertexHit;

            Vector2[] corners = GetBoundsCorners(definition);
            for (int i = 0; i < corners.Length; i++)
            {
                if ((corners[i] - world).sqrMagnitude <= handleTolerance * handleTolerance)
                    return LevelSelection.WorldBounds((WorldBoundsCorner)i);
            }

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
                    return glyph.Selection;
            }

            return HitZoneBody(definition, world);
        }

        /// <summary>命中当前选中区域的顶点手柄。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="world">世界坐标。</param>
        /// <param name="tolerance">世界单位下的命中半径。</param>
        /// <returns>命中的顶点选中项; 未命中时为 <see cref="LevelSelection.None"/>。</returns>
        private LevelSelection HitZoneVertex(LevelDefinition definition, Vector2 world, float tolerance)
        {
            if (!_selected.IsZone)
                return LevelSelection.None;
            ZoneData zone = ZoneAt(definition, _selected.ZoneKind, _selected.ZoneIndex);
            if (zone?.Vertices == null)
                return LevelSelection.None;
            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                PointData vertex = zone.Vertices[i];
                if (vertex == null)
                    continue;
                float dx = vertex.X - world.x;
                float dy = vertex.Y - world.y;
                if (dx * dx + dy * dy <= tolerance * tolerance)
                    return LevelSelection.ZoneVertex(_selected.ZoneKind, _selected.ZoneIndex, i);
            }
            return LevelSelection.None;
        }

        /// <summary>命中区域边或区域体; 边优先于面, 便于双击插入顶点。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="world">世界坐标。</param>
        /// <returns>命中的区域选中项; 未命中时为 <see cref="LevelSelection.None"/>。</returns>
        private LevelSelection HitZoneBody(LevelDefinition definition, Vector2 world)
        {
            float edgeTolerance = EdgeHitWidthPixels / Mathf.Max(_zoom, 0.0001f);
            if (HitZoneEdge(definition, ZoneKind.Forbidden, world, edgeTolerance, out LevelSelection edge))
                return edge;
            if (HitZoneEdge(definition, ZoneKind.Deployable, world, edgeTolerance, out edge))
                return edge;
            if (HitZoneInterior(definition, ZoneKind.Forbidden, world))
                return LevelSelection.Zone(
                    ZoneKind.Forbidden,
                    IndexOfZoneContaining(definition, ZoneKind.Forbidden, world)
                );
            if (HitZoneInterior(definition, ZoneKind.Deployable, world))
                return LevelSelection.Zone(
                    ZoneKind.Deployable,
                    IndexOfZoneContaining(definition, ZoneKind.Deployable, world)
                );
            return LevelSelection.None;
        }

        /// <summary>在指定种类的区域中命中边。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="kind">区域种类。</param>
        /// <param name="world">世界坐标。</param>
        /// <param name="tolerance">世界单位下的命中容差。</param>
        /// <param name="hit">命中的区域选中项。</param>
        /// <returns>命中时返回 true。</returns>
        private bool HitZoneEdge(
            LevelDefinition definition,
            ZoneKind kind,
            Vector2 world,
            float tolerance,
            out LevelSelection hit
        )
        {
            hit = LevelSelection.None;
            List<ZoneData> zones = kind == ZoneKind.Deployable ? definition.DeployableZones : definition.ForbiddenZones;
            if (zones == null)
                return false;
            for (int z = 0; z < zones.Count; z++)
            {
                List<PointData> vertices = zones[z]?.Vertices;
                if (vertices == null || vertices.Count < 2)
                    continue;
                for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
                {
                    PointData a = vertices[j];
                    PointData b = vertices[i];
                    if (a == null || b == null)
                        continue;
                    if (DistanceToSegment(world, new Vector2(a.X, a.Y), new Vector2(b.X, b.Y)) <= tolerance)
                    {
                        hit = LevelSelection.Zone(kind, z);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>判断指定种类的区域中是否存在包含该点的区域。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="kind">区域种类。</param>
        /// <param name="world">世界坐标。</param>
        /// <returns>存在包含该点的区域时返回 true。</returns>
        private static bool HitZoneInterior(LevelDefinition definition, ZoneKind kind, Vector2 world) =>
            IndexOfZoneContaining(definition, kind, world) >= 0;

        /// <summary>返回包含该点的区域下标。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="kind">区域种类。</param>
        /// <param name="world">世界坐标。</param>
        /// <returns>区域下标; 无命中时为 -1。</returns>
        private static int IndexOfZoneContaining(LevelDefinition definition, ZoneKind kind, Vector2 world)
        {
            List<ZoneData> zones = kind == ZoneKind.Deployable ? definition.DeployableZones : definition.ForbiddenZones;
            if (zones == null)
                return -1;
            for (int z = 0; z < zones.Count; z++)
            {
                if (ContainsPoint(zones[z], world))
                    return z;
            }
            return -1;
        }

        /// <summary>按射线法判断点是否落在区域内部; 仅用于编辑器拾取, 不作玩法判定。</summary>
        /// <param name="zone">区域。</param>
        /// <param name="point">世界坐标。</param>
        /// <returns>点在区域内部时返回 true。</returns>
        private static bool ContainsPoint(ZoneData zone, Vector2 point)
        {
            List<PointData> vertices = zone?.Vertices;
            if (vertices == null || vertices.Count < 3)
                return false;
            bool inside = false;
            for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
            {
                PointData current = vertices[i];
                PointData previous = vertices[j];
                if (current == null || previous == null)
                    return false;
                if ((current.Y > point.y) == (previous.Y > point.y))
                    continue;
                float x = previous.X + (point.y - previous.Y) * (current.X - previous.X) / (current.Y - previous.Y);
                if (point.x < x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>计算点到线段的距离。</summary>
        /// <param name="point">点。</param>
        /// <param name="a">线段起点。</param>
        /// <param name="b">线段终点。</param>
        /// <returns>点到线段的距离。</returns>
        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
                return (point - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            return (point - (a + segment * t)).magnitude;
        }

        /// <summary>判断选中项是否可通过拖动移动。</summary>
        /// <param name="selection">选中项。</param>
        /// <returns>可拖动时返回 true。</returns>
        private static bool IsDraggable(LevelSelection selection)
        {
            switch (selection.Kind)
            {
                case LevelSelectionKind.StageObject:
                case LevelSelectionKind.SpawnPoint:
                case LevelSelectionKind.GoalPoint:
                case LevelSelectionKind.Zone:
                case LevelSelectionKind.ZoneVertex:
                case LevelSelectionKind.WorldBoundsCorner:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>取出选中项当前的世界坐标锚点, 用于计算拖动偏移。</summary>
        /// <param name="selection">选中项。</param>
        /// <returns>锚点世界坐标; 无法定位时返回视口中心。</returns>
        private Vector2 ResolveAnchor(LevelSelection selection)
        {
            LevelDefinition definition = _data?.Definition;
            if (definition == null)
                return ViewCenter;
            switch (selection.Kind)
            {
                case LevelSelectionKind.SpawnPoint:
                    return definition.StartPoint == null
                        ? ViewCenter
                        : new Vector2(definition.StartPoint.PositionX, definition.StartPoint.PositionY);
                case LevelSelectionKind.GoalPoint:
                    return definition.GoalPoint == null
                        ? ViewCenter
                        : new Vector2(definition.GoalPoint.PositionX, definition.GoalPoint.PositionY);
                case LevelSelectionKind.WorldBoundsCorner:
                    return GetBoundsCorners(definition)[(int)selection.Corner];
                case LevelSelectionKind.ZoneVertex:
                {
                    ZoneData zone = ZoneAt(definition, selection.ZoneKind, selection.ZoneIndex);
                    if (zone?.Vertices == null || selection.VertexIndex >= zone.Vertices.Count)
                        return ViewCenter;
                    PointData vertex = zone.Vertices[selection.VertexIndex];
                    return vertex == null ? ViewCenter : new Vector2(vertex.X, vertex.Y);
                }
                case LevelSelectionKind.Zone:
                {
                    ZoneData zone = ZoneAt(definition, selection.ZoneKind, selection.ZoneIndex);
                    return zone == null ? ViewCenter : ZoneCenter(zone);
                }
                default:
                    foreach (StageObjectGlyph glyph in _glyphs)
                    {
                        if (glyph.Selection == selection)
                            return glyph.Position;
                    }
                    return ViewCenter;
            }
        }

        /// <summary>计算区域顶点集合的包围盒中心。</summary>
        /// <param name="zone">区域。</param>
        /// <returns>中心世界坐标; 顶点不可用时返回视口中心。</returns>
        private Vector2 ZoneCenter(ZoneData zone)
        {
            List<PointData> vertices = zone?.Vertices;
            if (vertices == null || vertices.Count == 0)
                return ViewCenter;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (PointData vertex in vertices)
            {
                if (vertex == null)
                    continue;
                minX = Mathf.Min(minX, vertex.X);
                minY = Mathf.Min(minY, vertex.Y);
                maxX = Mathf.Max(maxX, vertex.X);
                maxY = Mathf.Max(maxY, vertex.Y);
            }
            return minX > maxX ? ViewCenter : new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        /// <summary>按种类与下标取出区域。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="kind">区域种类。</param>
        /// <param name="zoneIndex">区域下标。</param>
        /// <returns>区域; 下标无效时为 null。</returns>
        private static ZoneData ZoneAt(LevelDefinition definition, ZoneKind kind, int zoneIndex)
        {
            List<ZoneData> zones = kind == ZoneKind.Deployable ? definition.DeployableZones : definition.ForbiddenZones;
            return zones == null || zoneIndex < 0 || zoneIndex >= zones.Count ? null : zones[zoneIndex];
        }

        /// <summary>取出世界边界四角; 边界缺失时返回四个零向量。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <returns>按 <see cref="WorldBoundsCorner"/> 顺序排列的四角。</returns>
        private static Vector2[] GetBoundsCorners(LevelDefinition definition)
        {
            BoundsData bounds = definition.WorldBounds;
            if (bounds == null)
                return new Vector2[4];
            return new[]
            {
                new Vector2(bounds.MinX, bounds.MinY),
                new Vector2(bounds.MaxX, bounds.MinY),
                new Vector2(bounds.MaxX, bounds.MaxY),
                new Vector2(bounds.MinX, bounds.MaxY),
            };
        }

        /// <summary>
        /// 在区域边上插入一个顶点。
        /// </summary>
        /// <param name="localPosition">鼠标在视口本地坐标系中的位置。</param>
        /// <returns>成功发出插入请求时返回 true。</returns>
        /// <remarks>
        /// 插入点取鼠标位置在边上的投影, 使新顶点落在原边上而非鼠标的任意位置;
        /// 这样"细分区段"不会意外改变区域轮廓。
        /// </remarks>
        private bool TryRequestVertexInsert(Vector2 localPosition)
        {
            LevelDefinition definition = _data?.Definition;
            if (definition == null)
                return false;
            Vector2 world = LocalToWorld(localPosition);
            float tolerance = EdgeHitWidthPixels / Mathf.Max(_zoom, 0.0001f);
            if (!TryFindEdge(definition, world, tolerance, out ViewportVertexInsertRequest request))
                return false;
            VertexInsertRequested?.Invoke(request);
            return true;
        }

        /// <summary>查找距离给定点最近的区域边, 用于插入顶点。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="world">世界坐标。</param>
        /// <param name="tolerance">世界单位下的命中容差。</param>
        /// <param name="request">插入请求; 未命中时为其默认值。</param>
        /// <returns>命中某条边时返回 true。</returns>
        private static bool TryFindEdge(
            LevelDefinition definition,
            Vector2 world,
            float tolerance,
            out ViewportVertexInsertRequest request
        )
        {
            request = default;
            ZoneKind[] kinds = { ZoneKind.Forbidden, ZoneKind.Deployable };
            foreach (ZoneKind kind in kinds)
            {
                List<ZoneData> zones =
                    kind == ZoneKind.Deployable ? definition.DeployableZones : definition.ForbiddenZones;
                if (zones == null)
                    continue;
                for (int z = 0; z < zones.Count; z++)
                {
                    List<PointData> vertices = zones[z]?.Vertices;
                    if (vertices == null || vertices.Count < 2)
                        continue;
                    for (int i = 1; i <= vertices.Count; i++)
                    {
                        PointData a = vertices[i - 1];
                        PointData b = vertices[i % vertices.Count];
                        if (a == null || b == null)
                            continue;
                        Vector2 start = new Vector2(a.X, a.Y);
                        if (DistanceToSegment(world, start, new Vector2(b.X, b.Y)) > tolerance)
                            continue;
                        // 插入到边的终点之前, 保证新顶点落在 start 与 b 之间。
                        request = new ViewportVertexInsertRequest(
                            kind,
                            z,
                            i % vertices.Count,
                            ProjectOntoSegment(world, start, new Vector2(b.X, b.Y))
                        );
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>把点投影到线段上。</summary>
        /// <param name="point">点。</param>
        /// <param name="a">线段起点。</param>
        /// <param name="b">线段终点。</param>
        /// <returns>投影点; 线段退化时返回起点。</returns>
        private static Vector2 ProjectOntoSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
                return a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            return a + segment * t;
        }

        /// <summary>视口的交互状态。</summary>
        private enum DragMode
        {
            /// <summary>未在拖动。</summary>
            None = 0,

            /// <summary>正在平移视图。</summary>
            Pan = 1,

            /// <summary>正在移动编辑目标。</summary>
            Item = 2,
        }

        /// <summary>起点在视口中的内部对象 ID; 不进入 Authoring 数据。</summary>
        public const string SpawnPointObjectId = "$spawn";

        /// <summary>终点在视口中的内部对象 ID; 不进入 Authoring 数据。</summary>
        public const string GoalPointObjectId = "$goal";

        /// <summary>绘制缓存中的一个占位图形。</summary>
        private sealed class StageObjectGlyph
        {
            /// <summary>对象稳定 ID; 起点与终点为内部标记。</summary>
            public string ObjectId;

            /// <summary>对应的选中项。</summary>
            public LevelSelection Selection;

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
