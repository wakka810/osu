// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModLateRelaxTest
    {
        [Test]
        public void TestConfiguredWindow()
        {
            var mod = new OsuModLateRelax();

            // OD9's 300 window is 25.5ms in this release, so the default range is +10.5ms to +25.5ms.
            Assert.That(mod.IsWithinConfiguredWindow(10.49, 25.5), Is.False);
            Assert.That(mod.IsWithinConfiguredWindow(10.5, 25.5), Is.True);
            Assert.That(mod.IsWithinConfiguredWindow(25.5, 25.5), Is.True);
            Assert.That(mod.IsWithinConfiguredWindow(25.51, 25.5), Is.False);

            mod.WindowStartBeforeLateEdge.Value = 5;
            mod.WindowEndBeforeLateEdge.Value = 15;

            // Reversed setting order is accepted and still represents the range +10.5ms to +20.5ms.
            Assert.That(mod.IsWithinConfiguredWindow(10.5, 25.5), Is.True);
            Assert.That(mod.IsWithinConfiguredWindow(20.5, 25.5), Is.True);
            Assert.That(mod.IsWithinConfiguredWindow(20.51, 25.5), Is.False);

            mod.WindowStartBeforeLateEdge.Value = 100;
            mod.WindowEndBeforeLateEdge.Value = 0;

            // A range wider than the late half of the 300 window is clamped to the perfect hit time.
            Assert.That(mod.IsWithinConfiguredWindow(-0.01, 25.5), Is.False);
            Assert.That(mod.IsWithinConfiguredWindow(0, 25.5), Is.True);
        }
    }
}
