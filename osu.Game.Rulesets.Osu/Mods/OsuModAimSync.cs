// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModAimSync : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableToDrawableHitObject, IUpdatableByPlayfield
    {
        public override string Name => "Aim Sync";
        public override string Acronym => "AYS";
        public override IconUsage? Icon => OsuIcon.ModStrictTracking;
        public override ModType Type => ModType.DifficultyIncrease;
        public override LocalisableString Description => "Miss if your first tap is not properly latched onto the current circle.";
        public override bool Ranked => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay),
            typeof(ModRelax),
            typeof(OsuModAutopilot),
            typeof(OsuModCinema),
        };

        [SettingSource("Latch duration", "How long the cursor must continuously remain inside the circle immediately before tapping.", 0)]
        public BindableDouble LatchDuration { get; } = new BindableDouble(15)
        {
            MinValue = 0,
            MaxValue = 50,
            Precision = 1,
        };

        [SettingSource("Timing tolerance", "Maximum absolute tap error in milliseconds. 0 uses the map's 300 window; larger values are clamped to it.", 1)]
        public BindableDouble TimingTolerance { get; } = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 50,
            Precision = 1,
        };

        [SettingSource("Only jumps", "Only enforce Aim Sync on jumps rather than every circle and slider head.", 2)]
        public BindableBool OnlyJumps { get; } = new BindableBool(true);

        [SettingSource("Minimum jump distance", "Minimum spacing in osu! pixels for a transition to count as a jump.", 3)]
        public BindableDouble MinimumJumpDistance { get; } = new BindableDouble(100)
        {
            MinValue = 0,
            MaxValue = 400,
            Precision = 5,
        };

        [SettingSource("Maximum jump interval", "Maximum time from the previous circle or slider head for a transition to count as a jump.", 4)]
        public BindableDouble MaximumJumpInterval { get; } = new BindableDouble(200)
        {
            MinValue = 50,
            MaxValue = 1000,
            Precision = 10,
        };

        private readonly Dictionary<DrawableHitCircle, CircleState> circles = new Dictionary<DrawableHitCircle, CircleState>();

        private DrawableOsuRuleset ruleset = null!;
        private IFrameStableClock gameplayClock = null!;

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            ruleset = (DrawableOsuRuleset)drawableRuleset;
            gameplayClock = drawableRuleset.FrameStableClock;
            ruleset.KeyBindingInputManager.Add(new InputInterceptor(this));
        }

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            if (drawable is not DrawableHitCircle circle)
                return;

            if (!circles.TryGetValue(circle, out CircleState? state))
            {
                state = new CircleState(circle);
                circles.Add(circle, state);
                circle.HitObjectApplied += _ => state.Reset();
            }

            state.Reset();
        }

        public void Update(Playfield playfield)
        {
            double time = gameplayClock.CurrentTime;

            foreach (CircleState state in circles.Values)
            {
                if (!state.IsCurrent || state.Circle.Judged)
                {
                    state.HoverStart = null;
                    continue;
                }

                if (state.Circle.HitArea.IsHovered)
                {
                    state.EverHovered = true;
                    state.HoverStart ??= time;
                }
                else
                {
                    state.HoverStart = null;
                }
            }
        }

        private bool handlePress(OsuAction action)
        {
            if (action != OsuAction.LeftButton && action != OsuAction.RightButton)
                return false;

            if (gameplayClock.IsRewinding)
                return false;

            double time = gameplayClock.CurrentTime;
            CircleState? candidate = findCandidate(time);

            if (candidate == null || !shouldApply(candidate))
                return false;

            if (!candidate.IsCurrent || candidate.HitObject == null)
                return false;

            var hitWindows = candidate.HitObject.HitWindows;
            if (hitWindows == null)
                return false;

            double hitError = time - candidate.HitObject.StartTime;
            double greatWindow = hitWindows.WindowFor(HitResult.Great);
            double timingWindow = TimingTolerance.Value <= 0
                ? greatWindow
                : Math.Min(TimingTolerance.Value, greatWindow);

            bool isHovered = candidate.Circle.HitArea.IsHovered;
            double hoverDuration = isHovered && candidate.HoverStart.HasValue
                ? Math.Max(0, time - candidate.HoverStart.Value)
                : 0;

            AimSyncFailure failure = EvaluateAttempt(
                hitError,
                timingWindow,
                isHovered,
                candidate.EverHovered,
                hoverDuration,
                LatchDuration.Value);

            if (failure == AimSyncFailure.None)
                return false;

            candidate.Circle.MissForcefully();
            ruleset.Cursor.FlashColour(colourFor(failure), 350, Easing.OutQuint);

            Logger.Log(
                $"Aim Sync miss ({failure}): object={candidate.HitObject.StartTime:0.##}ms error={hitError:+0.##;-0.##;0}ms latch={hoverDuration:0.##}/{LatchDuration.Value:0.##}ms",
                LoggingTarget.Runtime,
                LogLevel.Debug);

            // Consume the invalid first tap. The object is already judged as a miss, so it cannot be rescued by re-tapping.
            return true;
        }

        private CircleState? findCandidate(double time)
        {
            CircleState? best = null;
            double bestDistance = double.MaxValue;

            foreach (CircleState state in circles.Values)
            {
                if (!state.IsCurrent || state.Circle.Judged || state.HitObject?.HitWindows == null)
                    continue;

                double hitError = time - state.HitObject.StartTime;
                double candidateWindow = state.HitObject.HitWindows.WindowFor(HitResult.Meh);
                double distance = Math.Abs(hitError);

                if (distance > candidateWindow)
                    continue;

                if (distance < bestDistance ||
                    (Math.Abs(distance - bestDistance) < 0.001 && best?.HitObject != null && state.HitObject.StartTime < best.HitObject.StartTime))
                {
                    best = state;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool shouldApply(CircleState current)
        {
            if (!current.IsCurrent || current.HitObject == null)
                return false;

            if (!OnlyJumps.Value)
                return true;

            CircleState? previous = null;

            foreach (CircleState state in circles.Values)
            {
                if (!state.IsCurrent || state.HitObject == null || state.HitObject.StartTime >= current.HitObject.StartTime)
                    continue;

                if (previous?.HitObject == null || state.HitObject.StartTime > previous.HitObject.StartTime)
                    previous = state;
            }

            if (previous?.HitObject == null)
                return false;

            double interval = current.HitObject.StartTime - previous.HitObject.StartTime;
            if (interval <= 0 || interval > MaximumJumpInterval.Value)
                return false;

            double distance = Vector2.Distance(previous.HitObject.StackedPosition, current.HitObject.StackedPosition);
            return distance >= MinimumJumpDistance.Value;
        }

        internal static bool IsCurrentHitObject(DrawableHitCircle circle, OsuHitObject? hitObject)
            => hitObject != null && ReferenceEquals(((DrawableHitObject)circle).HitObject, hitObject);

        internal static AimSyncFailure EvaluateAttempt(
            double hitError,
            double timingWindow,
            bool isHovered,
            bool everHovered,
            double hoverDuration,
            double requiredLatchDuration)
        {
            if (Math.Abs(hitError) > timingWindow)
                return AimSyncFailure.Timing;

            if (!isHovered)
                return everHovered ? AimSyncFailure.EarlyExit : AimSyncFailure.MissAim;

            if (hoverDuration < requiredLatchDuration)
                return AimSyncFailure.NoLatch;

            return AimSyncFailure.None;
        }

        private static Colour4 colourFor(AimSyncFailure failure)
        {
            switch (failure)
            {
                case AimSyncFailure.Timing:
                    return Colour4.Yellow;

                case AimSyncFailure.NoLatch:
                    return Colour4.Orange;

                case AimSyncFailure.EarlyExit:
                    return Colour4.Red;

                default:
                    return Colour4.Violet;
            }
        }

        internal enum AimSyncFailure
        {
            None,
            Timing,
            MissAim,
            EarlyExit,
            NoLatch,
        }

        private sealed class CircleState
        {
            public readonly DrawableHitCircle Circle;

            public OsuHitObject? HitObject { get; private set; }
            public bool IsCurrent => IsCurrentHitObject(Circle, HitObject);

            public double? HoverStart;
            public bool EverHovered;

            public CircleState(DrawableHitCircle circle)
            {
                Circle = circle;
            }

            public void Reset()
            {
                HitObject = ((DrawableHitObject)Circle).HitObject as OsuHitObject;
                HoverStart = null;
                EverHovered = false;
            }
        }

        private partial class InputInterceptor : Component, IKeyBindingHandler<OsuAction>
        {
            private readonly OsuModAimSync mod;

            public InputInterceptor(OsuModAimSync mod)
            {
                this.mod = mod;
            }

            public bool OnPressed(KeyBindingPressEvent<OsuAction> e) => mod.handlePress(e.Action);

            public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
            {
            }
        }
    }
}
