// SPDX-License-Identifier: MIT

using Celeste.Core.Scheduling;
using Celeste.Graphics.Content;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.Crystalline.Platforms
{
// The source does not mark this type Pooled; its fixed texture path ignores the block's spritePath.
    public sealed class VitMoveBlockDebris(ISourceEngineContext engine)
        : MovingBlockDebrisState(((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, engine,
            ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy, "objects/vitMoveBlock/debris");
}
