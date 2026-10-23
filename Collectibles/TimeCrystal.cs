using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Display;
using Celeste.Mods.Crystalline.Runtime;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;

namespace Celeste.Mods.Crystalline.Collectibles
{
    [CustomEntity("vitellary/timecrystal")]
    [RuntimeTypeAlias("vitmod.TimeCrystal")]
    public sealed class TimeCrystal : SceneEntityState
    {
        private readonly IOriginalRefillProcess engine;
        private readonly TimeCrystalRuntime runtime;
        private readonly bool oneUse, immediate, untilDash;
        private readonly float stopLength, useTime, privateTimeScale;
        private readonly string[] entityTypesIgnore;
        private readonly Sprite sprite, flash;
        private readonly SpriteImage outline;
        private readonly WigglerComponent wiggler;
        private readonly BloomPointState bloom;
        private readonly VertexLightState light;
        private readonly SineWaveState sine;
        private OriginalLevelSceneRoot level;
        private float respawnTimer;

        public TimeCrystal(ISourceEngineContext context, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)context).EntitySourceDataRegistration, data.Position + offset)
        {
            engine = (IOriginalRefillProcess)context;
            runtime = TimeCrystalRuntime.Get(context);
            oneUse = data.Bool("oneUse");
            stopLength = data.Float("stopLength", 2);
            useTime = data.Float("respawnTime", 2.5f);
            immediate = data.Bool("immediate");
            untilDash = data.Bool("untilDash");
            entityTypesIgnore = data.Attr("entityTypesToIgnore").Split(',');
            privateTimeScale = data.Float("timeScale");
            Collider = new HitboxColliderGeometry(16, 16, -8, -8);
            Add(new PlayerColliderComponentState(OnPlayer));
            if (!oneUse)
            {
                Add(outline = new SpriteImage(engine.GameGraphics.GameAtlas["objects/crystals/time/outline"]));
                outline.CenterOrigin();
                outline.Visible = false;
            }

            string type = oneUse ? "idlenr" : "idle";
            Add(sprite = new Sprite(engine, engine.GameGraphics.GameAtlas,
                "objects/crystals/time/" + (untilDash ? "untildash/" : "") + type));
            sprite.AddLoop(type, "", .1f);
            sprite.Play(type);
            sprite.CenterOrigin();
            Add(flash = new Sprite(engine, engine.GameGraphics.GameAtlas, "objects/crystals/time/flash"));
            flash.Add("flash", "", .05f);
            flash.OnFinish = delegate { flash.Visible = false; };
            flash.CenterOrigin();
            Add(wiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(1, 4,
                value => sprite.Scale = flash.Scale = Vector2.One * (1 + value * .2f)));
            Add(new MirrorReflectionState());
            Add(bloom = new BloomPointState(.8f, 16));
            Add(light = new VertexLightState(engine, Vector2.Zero, PackedColor.White, 1, 16, 48));
            Add(sine = new SineWaveState(engine, .6f));
            sine.Randomize();
            UpdateY();
            Depth = -100;
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            level = (OriginalLevelSceneRoot)scene;
        }

        public override void Update()
        {
            base.Update();
            if (respawnTimer > 0)
            {
                respawnTimer -= engine.DeltaTime;
                if (respawnTimer <= 0) Respawn();
            }
            else if (level.OnInterval(.1f) && runtime.State.StopStage != 1)
                level.ParticlesFG.Emit(untilDash ? runtime.Particles.GlowUntilDash : runtime.Particles.Glow,
                    1, Position, Vector2.One * 5);

            UpdateY();
            light.Alpha = ScalarMath.Approach(light.Alpha, sprite.Visible ? 1 : 0, 4 * engine.DeltaTime,
                engine.ExtremaArithmetic);
            bloom.Alpha = light.Alpha * .8f;
            if (level.OnInterval(2) && sprite.Visible)
            {
                flash.Play("flash", restart: true);
                flash.Visible = true;
            }
        }

        private void Respawn()
        {
            if (Collidable) return;
            Collidable = true;
            sprite.Visible = true;
            outline.Visible = false;
            Depth = -100;
            wiggler.Start();
            engine.SpatialAudio.Play("event:/game/general/diamond_return", Position);
            level.ParticlesFG.Emit(untilDash ? runtime.Particles.RegenUntilDash : runtime.Particles.Regen, 16, Position,
                Vector2.One * 2);
        }

        private void UpdateY() => flash.Position.Y = sprite.Position.Y = bloom.Y = sine.Value * 2;

        public override void Render(ISourceRenderContext context)
        {
            if (sprite.Visible) sprite.DrawOutline(context);
            base.Render(context);
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            engine.SpatialAudio.Play("event:/game/general/diamond_touch", Position);
            engine.RumbleSession.Rumble(1, 1);
            Collidable = false;
            Add(new SimulationCoroutine(engine, RefillRoutine(player)));
            if (oneUse) RemoveSelf();
            else respawnTimer = useTime;
            var state = runtime.State;
            state.EntityTypesToIgnore = entityTypesIgnore;
            if (state.StopStage != 1)
            {
                if (untilDash)
                {
                    player.RefillDash();
                    state.Type = TimeStopState.FreezeType.UntilDash;
                }
                else state.StopTimer = stopLength;

                state.TimeScaleToSet = privateTimeScale;
                state.ScaleTimer = immediate ? 1 : 0;
                state.StopStage = 1;
            }
            else state.StopTimer += stopLength;
        }

        private IEnumerator RefillRoutine(RoomPlayerSession player)
        {
            engine.Freeze(.05f);
            yield return null;
            level.Shake(.3f);
            sprite.Visible = flash.Visible = false;
            if (!oneUse) outline.Visible = true;
            Depth = 8999;
            yield return .05f;
            var angle = Vector2Geometry.Angle(player.Speed);
            var particle = untilDash ? runtime.Particles.ShatterUntilDash : runtime.Particles.Shatter;
            level.ParticlesFG.Emit(particle, 5, Position, Vector2.One * 4, angle - (float)Math.PI / 2);
            level.ParticlesFG.Emit(particle, 5, Position, Vector2.One * 4, angle + (float)Math.PI / 2);
            OriginalSlashFxState.Burst(engine, Position, angle);
        }
    }
}
