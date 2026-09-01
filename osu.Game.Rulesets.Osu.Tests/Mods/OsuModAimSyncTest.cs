// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModAimSyncTest
    {

        [Test]
        public void TestUnappliedPooledDrawableIsNotCurrent()
        {
            var circle = new DrawableHitCircle();
            Assert.That(OsuModAimSync.IsCurrentHitObject(circle, null), Is.False);
        }

        [TestCase(0, 25.5, true, true, 15, 15, 0)]
        [TestCase(26, 25.5, true, true, 30, 15, 1)]
        [TestCase(0, 25.5, false, true, 0, 15, 3)]
        [TestCase(0, 25.5, false, false, 0, 15, 2)]
        [TestCase(0, 25.5, true, true, 14.99, 15, 4)]
        public void TestAttemptEvaluation(
            double hitError,
            double timingWindow,
            bool isHovered,
            bool everHovered,
            double hoverDuration,
            double latchDuration,
            int expected)
        {
            Assert.That(
                OsuModAimSync.EvaluateAttempt(hitError, timingWindow, isHovered, everHovered, hoverDuration, latchDuration),
                Is.EqualTo((OsuModAimSync.AimSyncFailure)expected));
        }
    }
}
