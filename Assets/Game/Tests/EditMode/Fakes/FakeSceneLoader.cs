#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Flow;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 内存场景加载器（测试替身）：不依赖 SceneManager，模拟延迟加载与失败.
    /// </summary>
    public sealed class FakeSceneLoader : ISceneLoader
    {
        private readonly HashSet<string> _loaded = new HashSet<string>();
        private readonly Dictionary<string, bool> _failLoads = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _failUnloads = new Dictionary<string, bool>();

        /// <summary>已加载的场景名集合.</summary>
        public IReadOnlyCollection<string> LoadedSceneNames => _loaded;

        /// <summary>最近一次加载请求的场景名（用于断言）.</summary>
        public string? LastLoadRequest { get; private set; }

        /// <summary>最近一次卸载请求的场景名（用于断言）.</summary>
        public string? LastUnloadRequest { get; private set; }

        /// <summary>加载阻塞延迟（毫秒），用于测试并发防重入.</summary>
        public int LoadDelayMs { get; set; }

        /// <summary>卸载阻塞延迟（毫秒）.</summary>
        public int UnloadDelayMs { get; set; }

        /// <summary>设置指定场景加载失败.</summary>
        public void FailLoad(string sceneName) => _failLoads[sceneName] = true;

        /// <summary>设置指定场景卸载失败.</summary>
        public void FailUnload(string sceneName) => _failUnloads[sceneName] = true;

        /// <summary>手动注入一个已加载场景（模拟其他来源）.</summary>
        public void SimulateLoaded(string sceneName) => _loaded.Add(sceneName);

        public async Task<bool> LoadAdditiveAsync(string sceneName, CancellationToken cancellationToken)
        {
            LastLoadRequest = sceneName;
            if (_loaded.Contains(sceneName))
            {
                return true;
            }

            if (LoadDelayMs > 0)
            {
                await Task.Delay(LoadDelayMs, cancellationToken);
            }

            if (_failLoads.ContainsKey(sceneName))
            {
                return false;
            }

            _loaded.Add(sceneName);
            return true;
        }

        public async Task<bool> UnloadAsync(string sceneName, CancellationToken cancellationToken)
        {
            LastUnloadRequest = sceneName;
            if (!_loaded.Contains(sceneName))
            {
                return true;
            }

            if (UnloadDelayMs > 0)
            {
                await Task.Delay(UnloadDelayMs, cancellationToken);
            }

            if (_failUnloads.ContainsKey(sceneName))
            {
                return false;
            }

            _loaded.Remove(sceneName);
            return true;
        }
    }
}
