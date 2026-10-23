using Celeste.Core.Display;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class TimeCrystalParticles
    {
        public readonly ParticleTypePresentation Shatter, Glow, Regen;
        public readonly ParticleTypePresentation ShatterUntilDash, GlowUntilDash, RegenUntilDash;

        public TimeCrystalParticles(IOriginalRefillProcess engine)
        {
            var source = engine.RefillParticles;
            Shatter = Copy(source.P_Shatter, PackedColor.FromHex("bababa"), PackedColor.FromHex("dedede"));
            Glow = Copy(source.P_Glow, PackedColor.FromHex("bababa"), PackedColor.FromHex("dedede"));
            Regen = Regeneration(Glow);
            ShatterUntilDash = Copy(source.P_Shatter, PackedColor.FromHex("aaddff"), PackedColor.FromHex("99aaff"));
            GlowUntilDash = Copy(source.P_Glow, PackedColor.FromHex("aaddff"), PackedColor.FromHex("99aaff"));
            RegenUntilDash = Regeneration(GlowUntilDash);
        }

        private static ParticleTypePresentation Copy(ParticleTypePresentation source, PackedColor first,
            PackedColor second)
            => new(source) { Color = first, Color2 = second };

        private static ParticleTypePresentation Regeneration(ParticleTypePresentation source)
            => new(source)
                { SpeedMin = 30, SpeedMax = 40, SpeedMultiplier = .2f, DirectionRange = (float)System.Math.PI * 2 };
    }
}
