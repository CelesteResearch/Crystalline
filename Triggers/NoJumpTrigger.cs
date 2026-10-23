// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.Crystalline.Triggers
{
    [CustomEntity("vitellary/nojumptrigger")]
    [SceneTracked]
    [RuntimeTypeAlias("vitmod.NoJumpTrigger")]
    public sealed class NoJumpTrigger : SourceTriggerEntityState
    {
        public NoJumpTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
        }
    }
}
