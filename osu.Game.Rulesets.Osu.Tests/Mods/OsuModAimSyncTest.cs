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

        [TestCase(158, 255.60125195311545, 37.376, 1, 0, OsuModAimSync.AllowanceUnit.Milliseconds, 41.51522104607587)]
        [TestCase(158, 255.60125195311545, 37.376, 1, 10, OsuModAimSync.AllowanceUnit.Milliseconds, 31.515221046075872)]
        [TestCase(158, 255.60125195311545, 37.376, 1, 40, OsuModAimSync.AllowanceUnit.Percent, 24.909132627645523)]
        [TestCase(237, 255.60125195311545, 37.376, 1.5, 0, OsuModAimSync.AllowanceUnit.Milliseconds, 62.272831569113805)]
        public void TestAdaptiveLatchDuration(
            double gameplayInterval,
            double distance,
            double radius,
            double gameplayRate,
            double allowance,
            OsuModAimSync.AllowanceUnit allowanceUnit,
            double expected)
        {
            Assert.That(
                OsuModAimSync.CalculateAdaptiveLatchDuration(gameplayInterval, distance, radius, gameplayRate, allowance, allowanceUnit),
                Is.EqualTo(expected).Within(0.001));
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
