// SPDX-License-Identifier: MIT

using Celeste.Core.Display;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Simulation.Presentation.Particles.Definitions;
using System;

namespace Celeste.Mods.Crystalline.Entities
{
    public sealed partial class ForceJumpCrystal
    {
        private sealed class ParticleSet
        {
            public readonly ParticleTypePresentation Shatter;
            public readonly ParticleTypePresentation Glow;
            public readonly ParticleTypePresentation Regen;

            public ParticleSet(IOriginalRefillProcess process)
            {
                Shatter = new ParticleTypePresentation(process.RefillParticles.P_Shatter)
                    { Color = new PackedColor(166, 181, 255), Color2 = new PackedColor(207, 216, 255) };
                Glow = new ParticleTypePresentation(process.RefillParticles.P_Glow)
                    { Color = new PackedColor(207, 236, 255), Color2 = new PackedColor(219, 222, 255) };
                Regen = new ParticleTypePresentation(Glow)
                    { SpeedMin = 30f, SpeedMax = 40f, SpeedMultiplier = .2f, DirectionRange = (float)Math.PI * 2f };
            }
        }
    }
}
