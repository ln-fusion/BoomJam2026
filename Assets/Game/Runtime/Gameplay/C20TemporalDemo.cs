using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>C20 独立验收场景入口；生成小车与 Box 的回溯、单向传送验证，不连接正式流程或存档。</summary>
    public sealed class C20TemporalDemo : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 10f)] private float rewindSeconds = 2f;
        private TemporalAbilityWorld _world;
        private TemporalTrigger2D _carTrigger;
        private TemporalTrigger2D _boxTrigger;
        private Sprite _square;
        private Texture2D _texture;
        private double _accumulator;
        private bool _running = true;
        private bool _rewind = true;
        private bool _blockHistory;
        private bool _blockerAdded;

        /// <summary>进入验证场景后创建相机和矩形显示资源，并开始回溯验证。</summary>
        private void Start()
        {
            _texture = new Texture2D(1, 1);
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _square = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            var cameraObject = new GameObject("C20 Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
            Restart(true);
        }

        /// <summary>渲染帧仅累加时间，最多推进八个固定 Tick；保留积压，严重落后时暂停验证。</summary>
        private void Update()
        {
            if (!_running || _world == null) return;
            _accumulator += Time.unscaledDeltaTime;
            if (_accumulator > 2) { _running = false; return; }
            int steps = 0;
            while (_accumulator >= 1d / TemporalAbilityWorld.TickRate && steps++ < 8)
            {
                if (_rewind && _blockHistory && !_blockerAdded && _world.CurrentTick >= 140)
                {
                    CreateSolid("History blocker", new Vector2(-4, 1.5f), Vector2.one, Color.red);
                    _blockerAdded = true;
                }
                _world.Step();
                _accumulator -= 1d / TemporalAbilityWorld.TickRate;
            }
        }

        /// <summary>显示验证操作、当前 Tick 和历史有效载荷；重建场景可以再次使用一次性回溯框。</summary>
        private void OnGUI()
        {
            if (_world == null) return;
            GUILayout.BeginArea(new Rect(16, 12, 580, 220), GUI.skin.box);
            GUILayout.Label("C20: top = Car, bottom = Box (independent bodies)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restart Rewind")) Restart(true);
            if (GUILayout.Button("Restart Teleport")) Restart(false);
            if (GUILayout.Button(_running ? "Pause" : "Resume")) { _running = !_running; _accumulator = 0; }
            GUILayout.EndHorizontal();
            _blockHistory = GUILayout.Toggle(_blockHistory, "Block Car history after it passes (rewind test)");
            GUILayout.Label("Tick: " + _world.CurrentTick + " | rewind: " + _world.RewindSeconds + "s | history payload: " + _world.HistoryPayloadBytes + " bytes");
            GUILayout.Label("Car: " + _carTrigger.LastResult + " | Box: " + _boxTrigger.LastResult);
            GUILayout.Label("Rewind: one use, even on failure. Teleport: one-way, reusable, unchanged velocity.");
            GUILayout.EndArea();
        }

        /// <summary>重建独立世界，生成两条互不影响的验证通道。</summary>
        /// <param name="rewind">为真验证回溯，否则验证传送。</param>
        private void Restart(bool rewind)
        {
            _world?.Unload();
            _world = new TemporalAbilityWorld(rewindSeconds);
            _rewind = rewind;
            _blockerAdded = false;
            _accumulator = 0;
            _running = true;
            CreateBody("Car", new Vector2(-6, 1.5f), new Vector2(0.8f, 0.5f), Color.cyan);
            CreateBody("Box", new Vector2(-6, -1.5f), new Vector2(0.6f, 0.6f), Color.yellow);
            _carTrigger = CreateTrigger("Car zone", 1.5f, rewind);
            _boxTrigger = CreateTrigger("Box zone", -1.5f, rewind);
            CreateSolid("End wall", new Vector2(7, 0), new Vector2(0.5f, 6), Color.gray);
        }

        /// <summary>创建可回溯和传送的动态矩形目标。</summary>
        /// <param name="id">稳定标识。</param>
        /// <param name="position">初始位置。</param>
        /// <param name="size">碰撞尺寸。</param>
        /// <param name="color">显示颜色。</param>
        private void CreateBody(string id, Vector2 position, Vector2 size, Color color)
        {
            GameObject root = CreateSolid(id, position, size, color);
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.velocity = new Vector2(2, 0);
            var target = root.AddComponent<TemporalBody2D>();
            target.Configure(id);
            _world.Register(target);
        }

        /// <summary>创建回溯框或入口及独立出口；出口没有反向传送能力。</summary>
        /// <param name="id">入口标识。</param>
        /// <param name="y">通道高度。</param>
        /// <param name="rewind">是否创建回溯框。</param>
        /// <returns>已注册的触发器。</returns>
        private TemporalTrigger2D CreateTrigger(string id, float y, bool rewind)
        {
            GameObject root = CreateSolid(id, new Vector2(rewind ? 1 : -1, y), new Vector2(1, 1.4f),
                new Color(0.7f, 0.2f, 1, 0.45f));
            Transform exit = null;
            if (!rewind)
            {
                GameObject marker = CreateSolid(id + " exit", new Vector2(3, y), new Vector2(0.15f, 1.4f), Color.green);
                marker.GetComponent<BoxCollider2D>().enabled = false;
                exit = marker.transform;
            }
            var trigger = root.AddComponent<TemporalTrigger2D>();
            trigger.Configure(id, rewind ? TemporalTriggerKind.Rewind : TemporalTriggerKind.Portal, exit);
            _world.Register(trigger);
            return trigger;
        }

        /// <summary>在本地世界创建带显示的矩形碰撞体。</summary>
        /// <param name="name">对象名。</param>
        /// <param name="position">世界位置。</param>
        /// <param name="size">尺寸。</param>
        /// <param name="color">颜色。</param>
        /// <returns>新根对象。</returns>
        private GameObject CreateSolid(string name, Vector2 position, Vector2 size, Color color)
        {
            var root = new GameObject(name, typeof(BoxCollider2D), typeof(SpriteRenderer));
            _world.AddRoot(root);
            root.transform.position = position;
            root.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = _square;
            renderer.color = color;
            return root;
        }

        /// <summary>离开验证场景时释放本地物理世界及临时显示资源。</summary>
        private void OnDestroy()
        {
            _world?.Unload();
            if (_square != null) Destroy(_square);
            if (_texture != null) Destroy(_texture);
        }
    }
}
