// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModLateRelax : OsuModRelax
    {
        public override string Name => "Late Relax";
        public override string Acronym => "LRX";
        public override LocalisableString Description => "Only clicks near the late edge of the 300 hit window.";

        [SettingSource("Window start", "Milliseconds before the late edge of the 300 window at which auto-tapping starts.", 0)]
        public BindableDouble WindowStartBeforeLateEdge { get; } = new BindableDouble(15)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1,
        };

        [SettingSource("Window end", "Milliseconds before the late edge of the 300 window at which auto-tapping stops. 0 is the late edge.", 1)]
        public BindableDouble WindowEndBeforeLateEdge { get; } = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1,
        };

        protected override bool CanHitCircle(DrawableHitCircle circle, double time)
        {
            if (!circle.HitArea.IsHovered || circle.HitObject.HitWindows == null)
                return false;

            double greatWindow = circle.HitObject.HitWindows.WindowFor(HitResult.Great);
            return IsWithinConfiguredWindow(time - circle.HitObject.StartTime, greatWindow);
        }

        internal bool IsWithinConfiguredWindow(double hitError, double greatWindow)
        {
            double startBeforeLateEdge = Math.Max(WindowStartBeforeLateEdge.Value, WindowEndBeforeLateEdge.Value);
            double endBeforeLateEdge = Math.Min(WindowStartBeforeLateEdge.Value, WindowEndBeforeLateEdge.Value);

            // Keep this training mod on the late side of the perfect hit only.
            double windowStart = Math.Max(0, greatWindow - startBeforeLateEdge);
            double windowEnd = Math.Max(0, greatWindow - endBeforeLateEdge);

            return hitError >= windowStart && hitError <= windowEnd;
        }
    }
}
