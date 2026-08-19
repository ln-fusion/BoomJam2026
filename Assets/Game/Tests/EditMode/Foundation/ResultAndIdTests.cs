using System;
using Game.Foundation.Ids;
using Game.Foundation.Results;
using NUnit.Framework;

namespace Game.Tests.EditMode.Foundation
{
    public sealed class ResultAndIdTests
    {
        [Test]
        public void StrongIds_WithSameTextAndDifferentTypes_DoNotShareAType()
        {
            var level = new LevelId("official.level.factory_001");
            var story = new StoryId("official.level.factory_001");

            Assert.That(level.ToString(), Is.EqualTo(story.ToString()));
            Assert.That(level.GetType(), Is.Not.EqualTo(story.GetType()));
        }

        [Test]
        public void StrongId_RejectsEmptyValue()
        {
            Assert.Throws<ArgumentException>(() => new LevelId(" "));
        }

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
