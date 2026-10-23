// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Display;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;

namespace Celeste.Mods.Crystalline.Entities
{
    [CustomEntity("vitellary/forcejumpcrystal")]
    [RuntimeTypeAlias("vitmod.ForceJumpCrystal")]
    public sealed partial class ForceJumpCrystal : SceneEntityState
    {
        private static readonly ConditionalWeakTable<IOriginalRefillProcess, ParticleSet> Particles = new();
        private readonly IOriginalRefillProcess engine;
        private readonly bool oneUse;
        private readonly float useTime;
        private readonly bool holdJump;
        private readonly Sprite[] sprites = new Sprite[2];
        private readonly Sprite[] flashes = new Sprite[2];
        private readonly SineWaveState[] sines = new SineWaveState[2];
        private readonly SpriteImage outline;
        private readonly WigglerComponent wiggler;
        private readonly BloomPointState bloom;
        private readonly VertexLightState light;
        private readonly ParticleSet particles;
        private float respawnTimer;

        public ForceJumpCrystal(ISourceEngineContext context, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)context).EntitySourceDataRegistration, data.Position + offset)
        {
            engine = (IOriginalRefillProcess)context;
            oneUse = data.Bool("oneUse");
            useTime = data.Float("respawnTime", 2.5f);
            holdJump = data.Bool("holdJump", true);
            // The installed 1.17.3 implementation reads onlyJumpIfPossible but never uses it.
            particles = Particles.GetValue(engine, process => new ParticleSet(process));
            string idle = oneUse ? "idlenr" : "idle";
            for (int index = 0; index < 2; index++)
            {
                var sprite = sprites[index] = new Sprite(engine, engine.GameGraphics.GameAtlas,
                    "objects/crystals/forcejump/" + idle);
                sprite.AddLoop("idle", "", .1f);
                sprite.Play("idle", randomizeFrame: true);
                sprite.CenterOrigin();
                sprite.Position.X = index == 0 ? -4f : 4f;
                sprite.FlipX = index == 1;
                Add(sprite);
                var flash = flashes[index] = new Sprite(engine, engine.GameGraphics.GameAtlas,
                    "objects/crystals/forcejump/flash");
                flash.Add("flash", "", .05f);
                flash.OnFinish = _ => flash.Visible = false;
                flash.CenterOrigin();
                flash.Position.X = sprite.Position.X;
                flash.FlipX = sprite.FlipX;
                Add(flash);
            }

            outline = new SpriteImage(engine.GameGraphics.GameAtlas["objects/crystals/forcejump/outline"]);
            outline.CenterOrigin();
            outline.Visible = false;
            Add(outline);
            for (int index = 0; index < 2; index++)
                Add(sines[index] = new SineWaveState(engine, .6f, (float)engine.Random.NextDouble()));
            Collider = new HitboxColliderGeometry(16f, 16f, -8f, -8f);
            Add(new PlayerColliderComponentState(OnPlayer));
            Add(wiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(1f, 4f, value =>
            {
                for (int index = 0; index < 2; index++)
                    sprites[index].Scale = flashes[index].Scale = Vector2.One * (1f + value * .2f);
            }));
            Add(new MirrorReflectionState());
            Add(bloom = new BloomPointState(.8f, 16f));
            Add(light = new VertexLightState(engine, Vector2.Zero, PackedColor.White, 1f, 16, 48));
            Depth = -100;
            UpdateSprite();
        }

        public override void Update()
        {
            base.Update();
            var level = (OriginalLevelSceneRoot)Scene;
            if (respawnTimer > 0f)
            {
                respawnTimer -= engine.DeltaTime;
                if (respawnTimer <= 0f) Respawn();
            }
            else if (level.SourceScene.Clock.OnInterval(.1f, engine))
                level.ParticlesFG.Emit(particles.Glow, 1, Position, Vector2.One * 5f);

            UpdateSprite();
            light.Alpha = ScalarMath.Approach(light.Alpha, sprites[0].Visible ? 1f : 0f,
                4f * engine.DeltaTime, engine.ExtremaArithmetic);
            bloom.Alpha = light.Alpha * .8f;
            if (level.SourceScene.Clock.OnInterval(2f, engine) && sprites[0].Visible)
                foreach (var flash in flashes)
                {
                    flash.Play("flash", restart: true);
                    flash.Visible = true;
                }
        }

        private void Respawn()
        {
            if (Collidable) return;
            Collidable = true;
            foreach (var sprite in sprites) sprite.Visible = true;
            outline.Visible = false;
            Depth = -100;
            wiggler.Start();
            engine.SpatialAudio.Play("event:/game/general/diamond_return", Position);
            ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(particles.Regen, 16, Position, Vector2.One * 2f);
        }

        private void UpdateSprite()
        {
            for (int index = 0; index < 2; index++)
                sprites[index].Position.Y = flashes[index].Position.Y = sines[index].Value;
        }

        public override void Render(ISourceRenderContext context)
        {
            foreach (var sprite in sprites)
                if (sprite.Visible)
                    sprite.DrawOutline(context);
            base.Render(context);
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            player.Machine.State = 0;
            if (player.DashAttacking)
            {
                if (player.Data.SuperWallJumpAngle)
                {
                    if (player.World.WallJumpCheck(-1)) player.SuperWallJump(1);
                    else if (player.World.WallJumpCheck(1)) player.SuperWallJump(-1);
                    else player.SuperWallJump(0);
                }
                else player.SuperJump();
            }
            else if (player.World.WallJumpCheck(player.Facing))
            {
                if (player.Input.GrabCheck && player.Data.Stamina > 0f && player.Holding.Holding == null &&
                    !ClimbBlockerState.Check(Scene, player, player.Position + Vector2.UnitX * 3f))
                    player.ClimbJump();
                else player.WallJump(-player.Facing);
            }
            else if (player.World.WallJumpCheck(-player.Facing)) player.WallJump(player.Facing);
            else player.Jump();

            player.Data.Motion.AutoJump = holdJump;
            engine.SpatialAudio.Play("event:/game/general/diamond_touch", Position);
            engine.RumbleSession.Rumble(1, 1);
            Collidable = false;
            Add(new SimulationCoroutine(engine, RefillRoutine(player)));
            if (!oneUse) respawnTimer = useTime;
        }

        private IEnumerator RefillRoutine(RoomPlayerSession player)
        {
            engine.Freeze(.05f);
            yield return null;
            var level = (OriginalLevelSceneRoot)Scene;
            level.Shake(.3f);
            for (int index = 0; index < 2; index++) sprites[index].Visible = flashes[index].Visible = false;
            if (!oneUse) outline.Visible = true;
            Depth = 8999;
            yield return .05f;
            float angle = Vector2Geometry.Angle(player.Speed);
            level.ParticlesFG.Emit(particles.Shatter, 5, Position, Vector2.One * 4f, angle - (float)Math.PI / 2f);
            level.ParticlesFG.Emit(particles.Shatter, 5, Position, Vector2.One * 4f, angle + (float)Math.PI / 2f);
            OriginalSlashFxState.Burst(engine, Position, angle);
            if (oneUse) RemoveSelf();
        }
    }
}
