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
    public sealed partial class ForceDashCrystal
    {
        public override void Update()
        {
            base.Update();
            if (respawnTimer > 0f)
            {
                respawnTimer -= engine.DeltaTime;
                if (respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
            else if (((OriginalLevelSceneRoot)Scene).SourceScene.Clock.OnInterval(0.1f, engine))
            {
                ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(particles.Glow, 1, Position, Vector2.One * 5f);
            }

            UpdateSprite();
            light.Alpha = ScalarMath.Approach(light.Alpha, sprites[1].Visible ? 1f : 0f, 4f * engine.DeltaTime,
                engine.ExtremaArithmetic);
            bloom.Alpha = light.Alpha * 0.8f;
            if (!((OriginalLevelSceneRoot)Scene).SourceScene.Clock.OnInterval(2f, engine) || !sprites[1].Visible)
            {
                return;
            }

            foreach (Sprite flash in flashes)
            {
                flash.Play("flash", true, false);
                flash.Visible = true;
            }
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            dirToUse = null;
        }

        public override void SceneEnd(object scene)
        {
            base.SceneEnd(scene);
            dirToUse = null;
        }

        private void Respawn()
        {
            if (Collidable)
            {
                return;
            }

            Collidable = true;
            foreach (Sprite sprite in sprites)
            {
                sprite.Visible = true;
            }

            outline.Visible = false;
            Depth = -100;
            wiggler.Start();
            engine.SpatialAudio.Play("event:/game/general/diamond_return", Position);
            ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(particles.Regen, 16, Position, Vector2.One * 2f);
        }

        private void UpdateSprite()
        {
            if (direction == "None")
            {
                for (int i = 0; i < 4; i++)
                {
                    flashes[i].Position = (sprites[i].Position = spritePositions[i] +
                                                                 vector.Rotate(new Vector2(1f, -1f),
                                                                     (float)i * (float)Math.PI / 2f) * sines[i].Value);
                }
            }
            else if (dirVector.X == 0f || dirVector.Y == 0f)
            {
                for (int j = 0; j < 3; j++)
                {
                    flashes[j].Position = (sprites[j].Position = spritePositions[j] + dirVector * sines[j].Value * 2f);
                }

                bloom.Position = dirVector * sines[0].Value * 2f;
            }
            else
            {
                for (int k = 0; k < 3; k++)
                {
                    flashes[k].Position = (sprites[k].Position = spritePositions[k] + dirVector * sines[k].Value);
                }

                bloom.Position = dirVector * sines[0].Value;
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            foreach (Sprite sprite in sprites)
            {
                if (sprite.Visible)
                {
                    sprite.DrawOutline(context);
                }
            }

            base.Render(context);
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (!needDash || player.Data.Dashes > 0)
            {
                player.Machine.State = 0;
                if (direction != "None")
                {
                    dirToUse = vector.Normalize(dirVector);
                }

                player.Machine.State = 2;
                if (needDash)
                {
                    player.Data.Dashes--;
                }

                engine.SpatialAudio.Play("event:/game/general/diamond_touch", Position);
                engine.RumbleSession.Rumble(1, 1);
                Collidable = false;
                Add(new SimulationCoroutine(engine, RefillRoutine(player)));
                if (!oneUse)
                {
                    respawnTimer = useTime;
                }
            }
        }

        private IEnumerator RefillRoutine(RoomPlayerSession player)
        {
            engine.Freeze(0.05f);
            yield return null;
            OriginalLevelSceneRoot level = ((OriginalLevelSceneRoot)Scene);
            level.Shake(0.3f);
            for (int i = 0; i < ((direction == "None") ? 4 : 3); i++)
            {
                Sprite obj = flashes[i];
                Sprite obj2 = sprites[i];
                bool visible = false;
                obj2.Visible = false;
                obj.Visible = visible;
            }

            if (!oneUse)
            {
                outline.Visible = true;
            }

            Depth = 8999;
            yield return 0.05f;
            Vector2? val = dirToUse;
            Vector2 val2 = vector.Normalize(dirVector);
            if (val.HasValue && val.GetValueOrDefault() == val2)
            {
                dirToUse = null;
            }

            float num = Vector2Geometry.Angle(player.Speed);
            level.ParticlesFG.Emit(particles.Shatter, 5, Position, Vector2.One * 4f, num - (float)Math.PI / 2f);
            level.ParticlesFG.Emit(particles.Shatter, 5, Position, Vector2.One * 4f, num + (float)Math.PI / 2f);
            OriginalSlashFxState.Burst(engine, Position, num);
            if (oneUse)
            {
                RemoveSelf();
            }
        }
    }
}
