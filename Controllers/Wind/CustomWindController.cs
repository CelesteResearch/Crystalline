using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Reflection;
using Celeste.Game.Entities.Environment.Wind;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Collectibles.Strawberries;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Environment.Core;

namespace Celeste.Mods.Crystalline.Controllers.Wind
{
    [SceneTracked]
    [RuntimeTypeAlias("vitmod.CustomWindController")]
    public sealed class CustomWindController : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly List<float> speedX;
        private readonly List<float> speedY;
        private readonly List<float> alternateSpeed;
        private readonly float catchupSpeed;
        private readonly bool loop;
        private bool active;
        private int windIndex;
        private Vector2 targetSpeed;
        private SimulationCoroutine coroutine;
        public string ActivationType { get; }

        public CustomWindController(ISourceEngineContext engine, List<float> speedX, List<float> speedY,
            List<float> alternateSpeed, float catchupSpeed, string activationType, bool loop, bool persist,
            bool fixUpdateDepth)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            this.speedX = speedX;
            this.speedY = speedY;
            this.alternateSpeed = alternateSpeed;
            this.catchupSpeed = catchupSpeed;
            ActivationType = activationType;
            this.loop = loop;
            OriginalGameTags tags = ((IOriginalGameTagsEnvironment)engine).Tags;
            AddTag(tags.TransitionUpdate.Value);
            if (persist) AddTag(tags.Global.Value | tags.Persistent.Value);
            if (fixUpdateDepth) Depth = 1;
        }

        public override void Update()
        {
            base.Update();
            CheckActivation();
            OriginalWindController.ApplyWind((OriginalLevelSceneRoot)Scene, targetSpeed,
                catchupSpeed * 1000f, engine);
        }

        private void CheckActivation()
        {
            OriginalLevelSceneRoot level = (OriginalLevelSceneRoot)Scene;
            var entities = level.SourceScene.Entities;
            switch (ActivationType)
            {
                case "Seeds":
                    bool seedCollected = false;
                    foreach (object entry in entities.GetTrackedEntities("Celeste.StrawberrySeed"))
                        if (((OriginalStrawberrySeedState)entry).Collected)
                        {
                            seedCollected = true;
                            break;
                        }

                    Type keySeedType = ((ISourceEverestReflectionEnvironment)engine).Reflection.GetFakeEntryAssembly()
                        .GetType("vitmod.KeyBerrySeed");
                    if (keySeedType != null && entities.TryGetTrackedEntities(keySeedType, out List<object> seeds))
                    {
                        FieldInfo collected = keySeedType.GetField("collected",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (object entry in seeds)
                            if ((bool)collected.GetValue(entry))
                            {
                                seedCollected = true;
                                break;
                            }
                    }

                    SetActive(seedCollected);
                    break;
                case "Strawberry":
                case "Keyberry":
                    RoomPlayerSession player = entities.GetTrackedEntity<RoomPlayerSession>();
                    bool hasBerry = false;
                    Type keyBerryType = ActivationType == "Keyberry"
                        ? ((ISourceEverestReflectionEnvironment)engine).Reflection.GetFakeEntryAssembly()
                        .GetType("vitmod.KeyBerry")
                        : null;
                    foreach (var follower in player.Leader.Followers)
                        if (ActivationType == "Strawberry"
                                ? follower.Entity is OriginalStrawberryState { Golden: false }
                                : keyBerryType != null && keyBerryType.IsInstanceOfType(follower.Entity))
                        {
                            hasBerry = true;
                            break;
                        }

                    SetActive(hasBerry);
                    break;
                case "Locked Door":
                case "Refill":
                case "Jellyfish":
                case "Theo":
                    break;
                case "Core Mode (Hot)":
                    SetActive(level.CoreMode == RoomCoreMode.Hot);
                    break;
                case "Core Mode (Cold)":
                    SetActive(level.CoreMode == RoomCoreMode.Cold);
                    break;
                case "Death":
                    RoomPlayerSession dyingPlayer = entities.GetTrackedEntity<RoomPlayerSession>();
                    if (dyingPlayer == null || dyingPlayer.Dead) ActivateWind();
                    break;
                default:
                    ActivateWind(debug: true);
                    break;
            }
        }

        private void SetActive(bool value)
        {
            if (value) ActivateWind();
            else DeactivateWind();
        }

        public void ActivateWind(bool debug = false)
        {
            if (active) return;
            active = true;
            if (debug && !string.IsNullOrEmpty(ActivationType))
                ((IEverestLoggerProcessEnvironment)engine).Logger.Info("CrystallineHelper/CustomWindController",
                    "Custom wind doesn't have a activation case for: " + ActivationType);
            if ((speedX.Count > 1 || speedY.Count > 1) && !alternateSpeed.Contains(0f))
            {
                Add(coroutine = new SimulationCoroutine(engine, WindRoutine()));
                return;
            }

            targetSpeed = new Vector2(speedX[0], speedY[0]) * 100f;
            SetAmbience();
        }

        public void DeactivateWind()
        {
            if (!active) return;
            active = false;
            if (coroutine != null) OwnedComponents.Remove(coroutine);
            targetSpeed = Vector2.Zero;
            SetAmbience();
        }

        public void SnapWind()
        {
            if (coroutine != null && coroutine.Active) coroutine.Update();
            ((OriginalLevelSceneRoot)Scene).Wind = targetSpeed;
            SetAmbience();
            windIndex = 0;
        }

        private void SetAmbience()
        {
            float direction = 0f;
            if (targetSpeed != Vector2.Zero)
            {
                float angle = (float)Math.Atan2(targetSpeed.Y, targetSpeed.X);
                angle += (float)Math.PI / 4f;
                angle %= (float)Math.PI * 2f;
                direction = angle < Math.PI ? 1f : -1f;
            }

            var output = ((IOriginalGameplayProcess)engine).AudioSession.ChannelOutput;
            output.SetAmbienceParam("wind_direction", direction);
            output.SetAmbienceParam("strong_wind", targetSpeed.LengthSquared() > 640000f ? 1f : 0f);
        }

        private IEnumerator WindRoutine()
        {
            while (loop || windIndex < Math.Max(speedX.Count, speedY.Count))
            {
                targetSpeed.X = speedX[windIndex % speedX.Count] * 100f;
                targetSpeed.Y = speedY[windIndex % speedY.Count] * 100f;
                SetAmbience();
                yield return alternateSpeed[windIndex % alternateSpeed.Count];
                windIndex++;
            }

            RemoveSelf();
        }
    }
}
