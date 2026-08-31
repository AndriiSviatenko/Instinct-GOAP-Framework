using Instinct.GOAP.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Instinct.GOAP.Tests
{
    public sealed class GoapGraphWindowTests
    {
        [Test]
        public void GoapMenuOpensAndBuildsGraphWindow()
        {
            var before = Resources.FindObjectsOfTypeAll<GoapGraphWindow>();

            Assert.That(EditorApplication.ExecuteMenuItem("GOAP/Graph Window"), Is.True);

            var windows = Resources.FindObjectsOfTypeAll<GoapGraphWindow>();
            Assert.That(windows, Has.Length.EqualTo(1));
            Assert.That(windows[0].titleContent.text, Is.EqualTo("GOAP Graph"));
            Assert.That(windows[0].rootVisualElement.childCount, Is.GreaterThan(0));

            if (before.Length == 0) windows[0].Close();
        }
    }
}
