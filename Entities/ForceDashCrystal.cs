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
using System.Collections.Generic;
using Celeste.Scene.Scenes;

namespace Celeste.Mods.Crystalline.Entities
{
    [CustomEntity("vitellary/forcedashcrystal")]
    [RuntimeTypeAlias("vitmod.ForceDashCrystal")]
    public sealed partial class ForceDashCrystal : SceneEntityState
    {
        private static readonly ConditionalWeakTable<IOriginalRefillProcess, ParticleSet> Particles = new();
        private readonly IOriginalRefillProcess engine;
        private readonly ISourceVectorArithmetic vector;
        private readonly ParticleSet particles;
        private readonly bool oneUse, needDash;
        private readonly string direction;
        private readonly float useTime;
        private readonly Vector2 dirVector;
        public static Vector2? dirToUse;
        private float respawnTimer;
        private readonly List<Sprite> sprites;
        private readonly List<Sprite> flashes;
        private readonly List<Vector2> spritePositions;
        private readonly List<SineWaveState> sines;
        private readonly SpriteImage outline;
        private readonly WigglerComponent wiggler;
        private readonly BloomPointState bloom;
        private readonly VertexLightState light;

        public ForceDashCrystal(ISourceEngineContext context, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)context).EntitySourceDataRegistration, data.Position + offset)
        {
            engine = (IOriginalRefillProcess)context;
            vector = ((ISourceVectorEnvironment)context).VectorArithmetic;
            particles = Particles.GetValue(engine, process => new ParticleSet(process));
            direction = data.Attr("direction", "Right");
            oneUse = data.Bool("oneUse", false);
            useTime = data.Float("respawnTime", 2.5f);
            needDash = data.Bool("needDash", false);
            string text = (needDash ? "needdash" : "dashless");
            string text2 = (oneUse ? "idlenr" : "idle");
            sprites = new List<Sprite>();
            spritePositions = new List<Vector2>();
            flashes = new List<Sprite>();
            sines = new List<SineWaveState>();
            switch (direction)
            {
                case "Downright":
                {
                    dirVector = Vector2.One;
                    for (int num7 = 0; num7 < 3; num7++)
                    {
                        Sprite val8 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/" + text2);
                        val8.AddLoop("idle", "", 0.1f);
                        val8.Play("idle", false, false);
                        val8.CenterOrigin();
                        val8.Rotation = (float)Math.PI / 2f;
                        this.Add(val8);
                        sprites.Add(val8);
                        Sprite flash8 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/flash");
                        flash8.Add("flash", "", 0.05f);
                        flash8.OnFinish = _ => { flash8.Visible = false; };
                        flash8.CenterOrigin();
                        flash8.Rotation = (float)Math.PI / 2f;
                        this.Add(flash8);
                        flashes.Add(flash8);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(2f, 2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(1f, -3f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(-3f, 1f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/diag/outline"]);
                    outline.CenterOrigin();
                    outline.Rotation = (float)Math.PI / 2f;
                    outline.Visible = false;
                    break;
                }
                case "Down":
                {
                    dirVector = Vector2.UnitY;
                    for (int num4 = 0; num4 < 3; num4++)
                    {
                        Sprite val5 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/" + text2);
                        val5.AddLoop("idle", "", 0.1f);
                        val5.Play("idle", false, false);
                        val5.CenterOrigin();
                        val5.Rotation = (float)Math.PI;
                        this.Add(val5);
                        sprites.Add(val5);
                        Sprite flash5 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/flash");
                        flash5.Add("flash", "", 0.05f);
                        flash5.OnFinish = _ => { flash5.Visible = false; };
                        flash5.CenterOrigin();
                        flash5.Rotation = (float)Math.PI;
                        this.Add(flash5);
                        flashes.Add(flash5);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(0f, 2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(4f, -2f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(-4f, -2f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/ortho/outline"]);
                    outline.CenterOrigin();
                    outline.Rotation = (float)Math.PI;
                    outline.Visible = false;
                    break;
                }
                case "Downleft":
                {
                    dirVector = new Vector2(-1f, 1f);
                    for (int num8 = 0; num8 < 3; num8++)
                    {
                        Sprite val9 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/" + text2);
                        val9.AddLoop("idle", "", 0.1f);
                        val9.Play("idle", false, false);
                        val9.CenterOrigin();
                        val9.Rotation = (float)Math.PI;
                        this.Add(val9);
                        sprites.Add(val9);
                        Sprite flash9 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/flash");
                        flash9.Add("flash", "", 0.05f);
                        flash9.OnFinish = _ => { flash9.Visible = false; };
                        flash9.CenterOrigin();
                        flash9.Rotation = (float)Math.PI;
                        this.Add(flash9);
                        flashes.Add(flash9);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(-2f, 2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(-1f, -3f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(3f, 1f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/diag/outline"]);
                    outline.CenterOrigin();
                    outline.Rotation = (float)Math.PI;
                    outline.Visible = false;
                    break;
                }
                case "Left":
                {
                    dirVector = -Vector2.UnitX;
                    for (int num2 = 0; num2 < 3; num2++)
                    {
                        Sprite val3 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/" + text2);
                        val3.AddLoop("idle", "", 0.1f);
                        val3.Play("idle", false, false);
                        val3.CenterOrigin();
                        val3.Rotation = -(float)Math.PI / 2f;
                        this.Add(val3);
                        sprites.Add(val3);
                        Sprite flash3 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/flash");
                        flash3.Add("flash", "", 0.05f);
                        flash3.OnFinish = _ => { flash3.Visible = false; };
                        flash3.CenterOrigin();
                        flash3.Rotation = -(float)Math.PI / 2f;
                        this.Add(flash3);
                        flashes.Add(flash3);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(-2f, 0f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(2f, -4f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(2f, 4f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/ortho/outline"]);
                    outline.CenterOrigin();
                    outline.Rotation = -(float)Math.PI / 2f;
                    outline.Visible = false;
                    break;
                }
                case "Upleft":
                {
                    dirVector = -Vector2.One;
                    for (int num5 = 0; num5 < 3; num5++)
                    {
                        Sprite val6 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/" + text2);
                        val6.AddLoop("idle", "", 0.1f);
                        val6.Play("idle", false, false);
                        val6.CenterOrigin();
                        val6.Rotation = -(float)Math.PI / 2f;
                        this.Add(val6);
                        sprites.Add(val6);
                        Sprite flash6 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/flash");
                        flash6.Add("flash", "", 0.05f);
                        flash6.OnFinish = _ => { flash6.Visible = false; };
                        flash6.CenterOrigin();
                        flash6.Rotation = -(float)Math.PI / 2f;
                        this.Add(flash6);
                        flashes.Add(flash6);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(-2f, -2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(-1f, 3f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(3f, -1f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/diag/outline"]);
                    outline.CenterOrigin();
                    outline.Rotation = -(float)Math.PI / 2f;
                    outline.Visible = false;
                    break;
                }
                case "Up":
                {
                    dirVector = -Vector2.UnitY;
                    for (int num = 0; num < 3; num++)
                    {
                        Sprite val2 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/" + text2);
                        val2.AddLoop("idle", "", 0.1f);
                        val2.Play("idle", false, false);
                        val2.CenterOrigin();
                        this.Add(val2);
                        sprites.Add(val2);
                        Sprite flash2 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/flash");
                        flash2.Add("flash", "", 0.05f);
                        flash2.OnFinish = _ => { flash2.Visible = false; };
                        flash2.CenterOrigin();
                        this.Add(flash2);
                        flashes.Add(flash2);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(0f, -2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(4f, 2f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(-4f, 2f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/ortho/outline"]);
                    outline.CenterOrigin();
                    outline.Visible = false;
                    break;
                }
                case "Upright":
                {
                    dirVector = new Vector2(1f, -1f);
                    for (int num6 = 0; num6 < 3; num6++)
                    {
                        Sprite val7 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/" + text2);
                        val7.AddLoop("idle", "", 0.1f);
                        val7.Play("idle", false, false);
                        val7.CenterOrigin();
                        this.Add(val7);
                        sprites.Add(val7);
                        Sprite flash7 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/flash");
                        flash7.Add("flash", "", 0.05f);
                        flash7.OnFinish = _ => { flash7.Visible = false; };
                        flash7.CenterOrigin();
                        this.Add(flash7);
                        flashes.Add(flash7);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(2f, -2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(1f, 3f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(-3f, -1f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/diag/outline"]);
                    outline.CenterOrigin();
                    outline.Visible = false;
                    break;
                }
                case "Right":
                {
                    dirVector = Vector2.UnitX;
                    for (int num3 = 0; num3 < 3; num3++)
                    {
                        Sprite val4 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/" + text2);
                        val4.AddLoop("idle", "", 0.1f);
                        val4.Play("idle", false, false);
                        val4.CenterOrigin();
                        val4.Rotation = -(float)Math.PI / 2f;
                        this.Add(val4);
                        sprites.Add(val4);
                        Sprite flash4 = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/ortho/flash");
                        flash4.Add("flash", "", 0.05f);
                        flash4.OnFinish = _ => { flash4.Visible = false; };
                        flash4.CenterOrigin();
                        flash4.Rotation = -(float)Math.PI / 2f;
                        this.Add(flash4);
                        flashes.Add(flash4);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(2f, 0f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(-2f, -4f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(-2f, 4f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/ortho/outline"]);
                    outline.CenterOrigin();
                    outline.Rotation = (float)Math.PI / 2f;
                    outline.Visible = false;
                    break;
                }
                default:
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Sprite val = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/" + text2);
                        val.AddLoop("idle", "", 0.1f);
                        val.Play("idle", false, false);
                        val.CenterOrigin();
                        val.Rotation = (float)i * (float)Math.PI / 2f;
                        this.Add(val);
                        sprites.Add(val);
                        Sprite flash = new Sprite(engine, engine.GameGraphics.GameAtlas,
                            "objects/crystals/forcedash/" + text + "/diag/flash");
                        flash.Add("flash", "", 0.05f);
                        flash.OnFinish = _ => { flash.Visible = false; };
                        flash.CenterOrigin();
                        flash.Rotation = (float)i * (float)Math.PI / 2f;
                        this.Add(flash);
                        flashes.Add(flash);
                    }

                    flashes[0].Position = (sprites[0].Position = new Vector2(2f, -2f));
                    flashes[1].Position = (sprites[1].Position = new Vector2(2f, 2f));
                    flashes[2].Position = (sprites[2].Position = new Vector2(-2f, 2f));
                    flashes[3].Position = (sprites[3].Position = new Vector2(-2f, -2f));
                    outline = new SpriteImage(
                        engine.GameGraphics.GameAtlas["objects/crystals/forcedash/" + text + "/diag/outline_none"]);
                    outline.CenterOrigin();
                    outline.Visible = false;
                    break;
                }
            }

            this.Add(outline);
            for (int num9 = 0; num9 < ((direction == "None") ? 4 : 3); num9++)
            {
                spritePositions.Add(sprites[num9].Position);
                sprites[num9].SetAnimationFrame(engine.Random.Next());
                SineWaveState val10 = new SineWaveState(engine, 0.6f, (float)engine.Random.NextDouble());
                this.Add(val10);
                sines.Add(val10);
            }

            this.Collider = new HitboxColliderGeometry(16f, 16f, -8f, -8f);
            this.Add(new PlayerColliderComponentState(OnPlayer));
            this.Add((wiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(1f, 4f, v =>
            {
                for (int j = 0; j < ((direction == "None") ? 4 : 3); j++)
                {
                    flashes[j].Scale = (sprites[j].Scale = Vector2.One * (1f + v * 0.2f));
                }
            })));
            this.Add(new MirrorReflectionState());
            BloomPointState val11 = new BloomPointState(0.8f, 16f);
            BloomPointState val12 = val11;
            bloom = val11;
            this.Add(val12);
            VertexLightState val13 = new VertexLightState(engine, Vector2.Zero, PackedColor.White, 1f, 16, 48);
            VertexLightState val14 = val13;
            light = val13;
            this.Add(val14);
            this.Depth = -100;
            UpdateSprite();
        }
    }
}
