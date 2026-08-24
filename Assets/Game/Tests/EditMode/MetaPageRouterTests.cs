#nullable enable
using System.Collections.Generic;
using Game.Contracts;
using Game.Presentation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// MetaPageRouter 测试：字符串解析与页面切换.
    /// </summary>
    public sealed class MetaPageRouterTests
    {
        [Test]
        public void FromString_UnknownValue_FallsBackToMap()
        {
            var router = new MetaPageRouter();
            Assert.That(router.FromString("not-a-page"), Is.EqualTo(MetaPageId.Map));
            Assert.That(router.FromString(""), Is.EqualTo(MetaPageId.Map));
        }

        [Test]
        public void FromString_KnownValue_ParsesCaseInsensitive()
        {
            var router = new MetaPageRouter();
            Assert.That(router.FromString("archive"), Is.EqualTo(MetaPageId.Archive));
            Assert.That(router.FromString("ARCHIVE"), Is.EqualTo(MetaPageId.Archive));
            Assert.That(router.FromString("lounge"), Is.EqualTo(MetaPageId.Lounge));
        }

        [Test]
        public void ToString_RoundTrips()
        {
            var router = new MetaPageRouter();
            foreach (
                var page in new List<MetaPageId>
                {
                    MetaPageId.Map,
                    MetaPageId.Archive,
                    MetaPageId.Character,
                    MetaPageId.Lounge,
                }
            )
            {
                Assert.That(router.FromString(router.ToString(page)), Is.EqualTo(page));
            }
        }

        [Test]
        public void Select_PublishesOnlyOnChange()
        {
            var router = new MetaPageRouter();
            var presenter = new MetaPagePresenter(router);
            var changes = new List<MetaPageId>();
            presenter.OnPageChanged += page => changes.Add(page);

            presenter.Select(MetaPageId.Archive);
            presenter.Select(MetaPageId.Archive);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(presenter.CurrentPage, Is.EqualTo(MetaPageId.Archive));
        }

        [Test]
        public void Restore_InvalidString_SelectsMap()
        {
            var router = new MetaPageRouter();
            var presenter = new MetaPagePresenter(router);

            presenter.Restore("???");
            Assert.That(presenter.CurrentPage, Is.EqualTo(MetaPageId.Map));
        }
    }
}
