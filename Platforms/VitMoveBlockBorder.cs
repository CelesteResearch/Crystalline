// SPDX-License-Identifier: MIT

using Celeste.Core.Components;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;

namespace Celeste.Mods.Crystalline.Platforms
{
    public sealed class VitMoveBlockBorder(VitMoveBlock parent, ISourceEntitySourceDataRegistration registration)
        : MovingBlockBorderState(parent, registration);
}
