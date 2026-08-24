using System;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Foundation
{
    /// <summary>
    /// Foundation 类型测试：强类型 ID 和 Result 的基本契约。
    /// </summary>
    public sealed class ResultAndIdTests
    {
        /// <summary>验证不同强类型 ID 即使文本相同也不会混淆。</summary>
        [Test]
        public void StrongIds_WithSameTextAndDifferentTypes_DoNotShareAType()
        {
            var level = new LevelId("official.level.factory_001");
            var story = new StoryId("official.level.factory_001");

            Assert.That(level.ToString(), Is.EqualTo(story.ToString()));
            Assert.That(level.GetType(), Is.Not.EqualTo(story.GetType()));
        }

        /// <summary>验证强类型 ID 拒绝空白值。</summary>
        [Test]
        public void StrongId_RejectsEmptyValue()
        {
            Assert.Throws<ArgumentException>(() => new LevelId(" "));
        }

        /// <summary>验证失败结果不会暴露成功值。</summary>
        [Test]
        public void FailedResult_DoesNotExposeValue()
        {
            var error = new ErrorCode(ErrorCategory.Validation, "test.invalid");
            Result<int> result = Result<int>.Failure(error);

            Assert.That(result.IsSuccess, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }
    }
}
