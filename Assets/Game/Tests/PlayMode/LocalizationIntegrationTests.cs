using System.Collections;
using System.Threading;
using Game.Foundation;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>验证 Unity Localization 资源在运行时的初始化、回退和 Locale 切换。</summary>
    public sealed class LocalizationIntegrationTests
    {
        /// <summary>加载正式 String Table，检查默认中文、英文切换和资源释放。</summary>
        [UnityTest]
        public IEnumerator DefaultLocalizationService_LoadsAndSwitchesStringTables()
        {
            var service = new DefaultLocalizationService();
            System.Threading.Tasks.Task<Result> initialization = service.InitializeAsync(
                CancellationToken.None);
            yield return new UnityEngine.WaitUntil(() => initialization.IsCompleted);

            Result initializationResult = initialization.GetAwaiter().GetResult();
            Assert.That(initializationResult.IsSuccess, Is.True,
                initializationResult.Message);
            Assert.That(service.IsInitialized, Is.True);
            Assert.That(service.CurrentLocaleCode, Is.EqualTo(DefaultLocalizationService.DefaultLocale));
            Assert.That(service.Get(new LocalizationKey(UiTextKeys.StartGame)),
                Is.EqualTo("开始游戏"));

            System.Threading.Tasks.Task<Result> switchTask = service.SetLocaleAsync(
                "en-US", CancellationToken.None);
            yield return new UnityEngine.WaitUntil(() => switchTask.IsCompleted);

            Result switchResult = switchTask.GetAwaiter().GetResult();
            Assert.That(switchResult.IsSuccess, Is.True, switchResult.Message);
            Assert.That(service.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(service.Get(new LocalizationKey(UiTextKeys.StartGame)),
                Is.EqualTo("Start Game"));

            service.Dispose();
            Assert.That(service.IsInitialized, Is.False);
        }
    }
}
