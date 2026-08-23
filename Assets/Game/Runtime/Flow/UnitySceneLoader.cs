using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Flow
{
    /// <summary>
    /// 基于 <see cref="SceneManager"/> 的场景加载器：Additive 加载、激活、卸载。
    /// </summary>
    public sealed class UnitySceneLoader : ISceneLoader
    {
        /// <summary>当前已加载的功能场景名（排除 Bootstrap 场景自身）.</summary>
        public IReadOnlyCollection<string> LoadedSceneNames
        {
            get
            {
                var names = new List<string>();
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene.name != SceneNames.Bootstrap)
                    {
                        names.Add(scene.name);
                    }
                }

                return names;
            }
        }

        /// <summary>Additive 加载并激活场景；取消或失败返回 false.</summary>
        /// <param name="sceneName">需要加载并激活的场景名。</param>
        /// <param name="cancellationToken">放弃等待加载完成的取消令牌。</param>
        /// <returns>场景已加载或成功完成加载时为 true；取消仅在需要等待加载时返回 false，失败时返回 false。</returns>
        public async Task<bool> LoadAdditiveAsync(string sceneName, CancellationToken cancellationToken)
        {
            if (IsLoaded(sceneName))
            {
                return true;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                return false;
            }

            if (!await AwaitAsyncOperation(op, cancellationToken))
            {
                return false;
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid())
            {
                return false;
            }

            // 新场景设为 Active，使新场景的 UI 获得输入焦点等场景级行为
            SceneManager.SetActiveScene(scene);
            return true;
        }

        /// <summary>卸载指定场景；未加载视为成功，取消返回 false.</summary>
        /// <param name="sceneName">需要卸载的场景名。</param>
        /// <param name="cancellationToken">放弃等待卸载完成的取消令牌。</param>
        /// <returns>场景未加载或成功完成卸载时为 true；取消仅在需要等待卸载时返回 false，失败时返回 false。</returns>
        public async Task<bool> UnloadAsync(string sceneName, CancellationToken cancellationToken)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return true;
            }

            var op = SceneManager.UnloadSceneAsync(scene);
            if (op == null)
            {
                return false;
            }

            return await AwaitAsyncOperation(op, cancellationToken);
        }

        /// <summary>等待 Unity 异步操作完成或外部取消。</summary>
        /// <param name="op">Unity 异步操作。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>操作完成返回 true；取消返回 false。</returns>
        private static async Task<bool> AwaitAsyncOperation(AsyncOperation op, CancellationToken cancellationToken)
        {
            // op.completed 只触发一次；取消时放弃等待并返回 false（SceneManager 不支持中途取消）
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            op.completed += _ => tcs.TrySetResult(true);
            using var registration = cancellationToken.Register(() => tcs.TrySetResult(false));
            return await tcs.Task;
        }

        /// <summary>检查指定场景是否已经加载。</summary>
        /// <param name="sceneName">场景名。</param>
        /// <returns>已加载返回 true，否则返回 false。</returns>
        private static bool IsLoaded(string sceneName)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
