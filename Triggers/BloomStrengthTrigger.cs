// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.Crystalline.Triggers
{
    [CustomEntity("vitellary/bloomstrengthtrigger")]
    [RuntimeTypeAlias("vitmod.BloomStrengthTrigger")]
    public sealed class BloomStrengthTrigger : SourceTriggerEntityState
    {
        private readonly float bloomFrom;
        private readonly float bloomTo;
        private readonly PositionModes positionMode;

        public BloomStrengthTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            bloomFrom = data.Float("bloomStrengthFrom", 1f);
            bloomTo = data.Float("bloomStrengthTo", 1f);
            positionMode = data.Enum("positionMode", PositionModes.NoEffect);
        }

        public override void OnStay(RoomPlayerSession player)
        {
            ((OriginalLevelSceneRoot)Scene).Bloom.Strength = bloomFrom + (bloomTo - bloomFrom)
                * ScalarMath.Clamp(GetPositionLerp(player, positionMode), 0f, 1f);
        }
    }
}
