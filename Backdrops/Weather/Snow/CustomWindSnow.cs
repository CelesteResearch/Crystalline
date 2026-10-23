// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Scene.Components.Oscillation;
using Celeste.Simulation.Presentation.Backdrops.Runtime;

namespace Celeste.Mods.Crystalline.Backdrops.Weather.Snow
{
    [RuntimeTypeAlias("vitmod.CustomWindSnow")]
    public class CustomWindSnow : BackdropPresentation
    {
        private readonly ISourceEngineContext engine;
        private readonly List<PackedColor> colors = new();
        private readonly List<float> alphas = new();
        private readonly int amount;
        private readonly Vector2 addSpeed;
        private Vector2 windVector;
        private readonly bool ignoreWind;
        public Vector2 CameraOffset;
        private readonly Vector2[] positions;
        private readonly int[] particleColors;
        private readonly SineWaveState[] sines;
        private Vector2 scale;
        private float rotation;
        private float visibleFade;

        public CustomWindSnow(ISourceEngineContext engine, string colors, string alphas, int amount, float speedX,
            float speedY, bool ignoreWind)
        {
            this.engine = engine;
            foreach (string text in colors.Split(','))
                this.colors.Add(new PackedColor(SourceColorParsing.HexToColor(text)));
            foreach (string text in alphas.Split(',')) this.alphas.Add(float.Parse(text));
            this.amount = amount;
            addSpeed = new Vector2(speedX * 100f, speedY * 100f);
            this.ignoreWind = ignoreWind;
            Color = PackedColor.White;
            CameraOffset = Vector2.Zero;
            scale = Vector2.One;
            rotation = 0f;
            visibleFade = 1f;
            positions = new Vector2[amount];
            particleColors = new int[amount];
            var colorRandom = ((ISourceRandomProcessEnvironment)engine).RandomProcess.Factory.CreateUnseeded();
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, Vector2.Zero,
                    new Vector2(640f, 360f));
                particleColors[i] = colorRandom.Next(0, this.colors.Count);
            }

            sines = new SineWaveState[amount / 15];
            for (int i = 0; i < sines.Length; i++)
            {
                sines[i] = new SineWaveState(engine,
                    SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, .8f, 1.2f));
                sines[i].Randomize();
            }
        }

        public override void Update(ISourceBackdropScene scene)
        {
            base.Update(scene);
            var extrema = ((ISourceExtremaEnvironment)engine).ExtremaArithmetic;
            visibleFade = ScalarMath.Approach(visibleFade, IsVisible(scene) ? 1f : 0f, engine.DeltaTime * 2f, extrema);
            for (int i = 0; i < sines.Length; i++) sines[i].Update();
            windVector = ignoreWind ? Vector2.Zero : scene.Wind;
            windVector += addSpeed;
            scale.X = Math.Max(1f, Math.Abs(windVector.Length()) / 100f);
            float angleFraction = (float)Math.Abs(Math.Sin(Math.Atan2(windVector.Y, windVector.X)));
            scale.X /= Math.Max(1f - angleFraction, .4f);
            scale.Y = 1f / Math.Max(1f, scale.X * .25f);
            rotation %= (float)Math.PI * 2f;
            float target = (float)Math.Atan2(windVector.Y, windVector.X);
            if ((double)Math.Abs(rotation - target) == Math.PI) rotation = target;
            else if ((double)(rotation - target) > Math.PI) target += (float)Math.PI * 2f;
            else if ((double)(target - rotation) > Math.PI) target -= (float)Math.PI * 2f;
            rotation = ScalarMath.Approach(rotation, target, engine.DeltaTime * 8f, extrema);
            if (windVector.X == 0f) windVector.Y *= 3f;
            for (int i = 0; i < positions.Length; i++)
            {
                float value = sines[i % sines.Length].Value;
                var velocity = new Vector2(windVector.X + value * 10f, windVector.Y + value * 10f);
                if (windVector.Length() == 0f) velocity.Y += 20f;
                positions[i] += velocity * engine.DeltaTime;
            }
        }

        public override void Render(ISourceRenderContext context, ISourceBackdropScene scene)
        {
            base.Render(context, scene);
            float countFactor = (float)Math.Abs(Math.Sin(Math.Atan2(windVector.Y, windVector.X)));
            if (windVector == Vector2.Zero) countFactor = 0f;
            countFactor = .6f + (1f - countFactor) * .4f;
            for (int i = 0; i < positions.Length; i++)
            {
                var color = colors[particleColors[i]];
                color *= visibleFade * (alphas.Count != colors.Count ? alphas[0] : alphas[particleColors[i]]);
                var position = positions[i];
                position.Y -= scene.Camera.Y + CameraOffset.Y;
                position.Y %= 360f;
                if (position.Y < 0f) position.Y += 360f;
                position.X -= scene.Camera.X + CameraOffset.X;
                position.X %= 640f;
                if (position.X < 0f) position.X += 640f;
                if ((float)i < (float)amount * countFactor)
                    for (int y = 0; y < scene.Camera.Viewport.Height; y += 360)
                    for (int x = 0; x < scene.Camera.Viewport.Width; x += 640)
                    {
                        var texture = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas["particles/snow"];
                        var draw = new AtlasTextureDraw(texture, position + new Vector2(x, y), texture.Center, color,
                            scale, rotation);
                        ((ISourceRenderCommandSink<AtlasTextureDraw>)context).Draw(in draw, null);
                    }
            }
        }
    }
}
