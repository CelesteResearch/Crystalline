// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Graphics.Display.Colors;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.Crystalline.Platforms
{
    [CustomEntity("vitellary/vitmoveblock")]
    [RuntimeTypeAlias("vitmod.VitMoveBlock")]
    public sealed class VitMoveBlock : MovingBlockState
    {
        private readonly float moveSpeed;
        private readonly bool canActivate;
        private readonly ParticleTypePresentation activateParticle;
        private readonly ParticleTypePresentation breakParticle;
        private readonly ParticleTypePresentation moveParticle;
        public readonly int Remote;
        public bool HeldTopButton { get; private set; }
        public bool HeldSideButton { get; private set; }

        public static VitMoveBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.SolidWorldFactory);

        public VitMoveBlock(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
            : base(data.Position + offset, data.Width, data.Height, data.Enum<MoveBlockDirection>("direction"),
                data.Bool("canSteer"), false, worlds.Create(data.Position + offset, false), CreateVisuals(data))
        {
            Remote = data.Int("remote");
            canActivate = data.Bool("canActivate", true);
            moveSpeed = data.Float("moveSpeed", 75);
            activateParticle = new ParticleTypePresentation
            {
                Size = 1, Color = PackedColor.Black, FadeMode = ParticleTypePresentation.FadeModes.Late,
                DirectionRange = 0.34906584f, LifeMin = 0.4f, LifeMax = 0.6f, SpeedMin = 20, SpeedMax = 40,
                SpeedMultiplier = 0.25f
            };
            breakParticle = new ParticleTypePresentation(activateParticle);
            moveParticle = new ParticleTypePresentation(activateParticle);
        }

        private static MoveBlockVisuals CreateVisuals(EntityData data)
        {
            var remote = data.Int("remote");

            PackedColor Color(string name, string fallback) =>
                new(SourceColorParsing.HexToColor(data.Attr(name, fallback)));

            return new MoveBlockVisuals(data.Attr("spritePath", "objects/vitMoveBlock"), -9000,
                remote == 0 ? Color("idleSingleColor", "465eb5") : Color("idleLinkedColor", "9e45b2"),
                remote == 0 ? Color("activeSingleColor", "4fd6ff") : Color("activeLinkedColor", "ff8cf5"),
                Color("breakingColor", "cc2541"), default);
        }

        protected override bool CanActivateFromRider() => !(Remote > 0 && !canActivate) && HasPlayerRider();
        protected override float MovementSpeed => moveSpeed;

        // Original VitMoveBlock inherits Solid's empty callback, not vanilla MoveBlock's activation callback.
        public override void OnStaticMoverTrigger(SourceStaticMoverState mover)
        {
        }

        // Brokemia's opt-in MoveCheck detour must stay on this helper, not the common core method.
        public override bool MoveCheck(Vector2 amount) => base.MoveCheck(amount);

        protected override void OnActivated()
        {
            if (Remote <= 0) return;
            foreach (var entity in ((OriginalLevelSceneRoot)Scene).Scheduler.Entities)
                if (entity is VitMoveBlock block && block.Remote == Remote)
                    block.Triggered = true;
        }

        protected override bool GetSteeringRider()
        {
            HeldTopButton = HeldSideButton = false;
            bool held;
            if (Horizontal)
            {
                held = HasPlayerOnTop();
                HeldTopButton = HasPlayerOnTop();
            }
            else
            {
                held = HasPlayerClimbing();
                HeldSideButton = HasPlayerClimbing();
            }

            if (held) return true;
            foreach (var entity in ((OriginalLevelSceneRoot)Scene).Scheduler.Entities)
                if (entity is VitMoveBlock block && block.Remote == Remote &&
                    block.State == MoveBlockMovementState.Moving
                    && (Horizontal ? block.HeldTopButton : block.HeldSideButton))
                    return true;
            return false;
        }

        protected override MovingBlockDebrisState CreateDebris(Vector2 position, Vector2 center, Vector2 home) =>
            new VitMoveBlockDebris(SourceWorld.EngineContext).Init(position, center, home);

        protected override MovingBlockBorderState CreateBorder(
            Celeste.Core.Components.ISourceEntitySourceDataRegistration registration) =>
            new VitMoveBlockBorder(this, registration);

        protected override void Emit(MoveBlockParticle particle, int count, Vector2 position, Vector2 range,
            float direction)
        {
            var level = (OriginalLevelSceneRoot)Scene;
            switch (particle)
            {
                case MoveBlockParticle.Activate:
                    level.ParticlesBG.Emit(activateParticle, count, position, range, direction); break;
                case MoveBlockParticle.Move:
                    level.ParticlesBG.Emit(moveParticle, count, position, range, direction); break;
                case MoveBlockParticle.Break:
                    level.Particles.Emit(breakParticle, count, position, range, direction); break;
                case MoveBlockParticle.Scrape:
                    level.ParticlesFG.Emit(
                        ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                        position);
                    break;
            }
        }
    }
}
