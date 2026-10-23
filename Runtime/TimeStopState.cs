using System;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class TimeStopState
    {
        public enum FreezeType
        {
            Timer,
            UntilDash
        }

        public float StopTimer;
        public int StopStage;
        public float TimeScaleToSet;
        public float ScaleTimer;
        public FreezeType Type;
        public string[] EntityTypesToIgnore = Array.Empty<string>();
        public bool UseScaledDelta { get; private set; }
        public float ScaledDelta { get; private set; }
        public float ScaledRawDelta { get; private set; }

        public void Advance(bool paused, float delta, float rawDelta, int? playerDashes)
        {
            if (!paused)
            {
                if (StopTimer > 0)
                {
                    StopTimer -= delta;
                    if (Type == FreezeType.Timer && StopTimer <= 0)
                    {
                        if (StopStage == 1)
                        {
                            StopTimer = 2;
                            StopStage = 2;
                            ScaleTimer = TimeScaleToSet;
                        }
                        else if (StopStage == 2)
                        {
                            StopTimer = 0;
                            StopStage = 0;
                            ScaleTimer = 0;
                        }
                    }
                }

                if (Type == FreezeType.UntilDash)
                {
                    // The installed implementation decrements a positive UntilDash timer here again.
                    if (StopTimer > 0) StopTimer -= delta;
                    if (StopTimer <= 0 && playerDashes.HasValue && playerDashes.Value < 1)
                    {
                        StopTimer = 2;
                        StopStage = 2;
                        Type = FreezeType.Timer;
                        ScaleTimer = TimeScaleToSet;
                    }
                }
            }

            if (StopStage > 0)
            {
                if (!paused) ScaleTimer += TimeScaleToSet < 1 ? delta : -delta;
                var scale = StopStage == 1 ? Math.Max(TimeScaleToSet, 1 - ScaleTimer / .5f)
                    : StopStage == 2 ? Math.Min(1, ScaleTimer / .5f) : 1;
                UseScaledDelta = scale != 1;
                if (UseScaledDelta)
                {
                    ScaledDelta = delta * scale;
                    ScaledRawDelta = rawDelta * scale;
                }
            }
            else UseScaledDelta = false;
        }

        public void Reset(bool afterDeath)
        {
            StopTimer = 0;
            StopStage = 0;
            ScaleTimer = 0;
            if (afterDeath) Type = FreezeType.Timer;
            else UseScaledDelta = false;
        }
    }
}
