using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Rendering;
using Celeste.Core.Components.Timing;
using Celeste.Core.Rendering.Renderers;
using Celeste.Core.Scheduling;
using Celeste.Everest.Reflection;
using Celeste.Game.Entities.Hazards.Spinners.Crystal;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Game.Gameplay.Mechanisms.Core;
using Celeste.Mods.Crystalline.Collectibles;
using Celeste.Scene.Entities;
using Celeste.Scene.Rendering;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Hazards.Lightning;
using Celeste.Simulation.Presentation.Particles;
using Celeste.Simulation.Presentation.Player.Death;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class TimeCrystalRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, TimeCrystalRuntime> runtimes = new();
        private static readonly List<IDisposable> hooks = new();
        private static int users;
        private readonly ISourceEngineContext engine;
        private readonly Dictionary<Type, bool> immuneTypes = new();
        private readonly Dictionary<Type, bool> ignoredTypes = new();
        private string[] ignoreSnapshot;
        private bool disposed;
        public TimeStopState State { get; } = new();
        public TimeCrystalParticles Particles { get; private set; }

        public TimeCrystalRuntime(ISourceEngineContext engine)
        {
            this.engine = engine;
            runtimes.Add(engine, this);
            try
            {
                if (users == 0) InstallHooks();
                users++;
            }
            catch
            {
                runtimes.Remove(engine);
                foreach (var hook in hooks) hook.Dispose();
                hooks.Clear();
                throw;
            }
        }

        public static TimeCrystalRuntime Get(ISourceEngineContext engine) => runtimes.TryGetValue(engine, out var value)
            ? value
            : throw new InvalidOperationException("Crystalline time crystal runtime is not loaded in this engine.");

        public void LoadContent() => Particles = new TimeCrystalParticles((IOriginalRefillProcess)engine);

        private static void InstallHooks()
        {
            hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Update)),
                (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)UpdateLevel));
            hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.End)),
                (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)EndLevel));
            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Die)),
                (Func<Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation>, RoomPlayerSession, Vector2,
                    bool, bool, PlayerDeathPresentation>)PlayerDie));
            hooks.Add(new Hook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.WallBoosterCheck)),
                (Func<Func<RoomPlayerWorld, bool>, RoomPlayerWorld, bool>)WallBoosterCheck));
            hooks.Add(new Hook(
                typeof(OriginalCoreModeToggleState).GetMethod("OnPlayer",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                (Action<Action<OriginalCoreModeToggleState, RoomPlayerSession>, OriginalCoreModeToggleState,
                    RoomPlayerSession>)CoreToggle));
            hooks.Add(new Hook(typeof(LightningRendererState).GetMethod(nameof(LightningRendererState.Update)),
                (Action<Action<LightningRendererState>, LightningRendererState>)LightningUpdate));
            hooks.Add(new Hook(
                typeof(OriginalCrystalStaticSpinner).GetMethod(nameof(OriginalCrystalStaticSpinner.UpdateHue)),
                (Action<Action<OriginalCrystalStaticSpinner>, OriginalCrystalStaticSpinner>)SpinnerHue));
            hooks.Add(new ILHook(typeof(SceneEntityRegistry).GetMethod(nameof(SceneEntityRegistry.Update)),
                PatchEntities));
            hooks.Add(new ILHook(typeof(SceneRendererList).GetMethod(nameof(SceneRendererList.Update)),
                PatchRenderers));
        }

        private static void PatchEntities(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.MatchCallvirt<ISourceEntityUpdate>("Update")))
                throw new InvalidOperationException(
                    "The real entity update call is missing from SceneEntityRegistry.Update.");
            cursor.Remove();
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Action<ISourceEntityUpdate, ISourceEngineContext>>(UpdateEntity);
        }

        private static void PatchRenderers(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.MatchCallvirt<ISceneRenderer>("Update")))
                throw new InvalidOperationException(
                    "The real renderer update call is missing from SceneRendererList.Update.");
            cursor.Remove();
            cursor.EmitDelegate<Action<ISceneRenderer, object>>(UpdateRenderer);
        }

        private static void UpdateLevel(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            if (runtimes.TryGetValue(level.EngineContext, out var runtime))
            {
                var player = level.Scheduler.Entities.GetTrackedEntity<RoomPlayerSession>();
                var state = runtime.State;
                state.Advance(level.Paused, runtime.engine.DeltaTime, runtime.engine.RawDeltaTime, player?.Data.Dashes);
                level.Session.SetCounter("Crystalline_TimeCrystal_StopTimer",
                    state.StopStage <= 1 ? (int)state.StopTimer : 0);
                level.Session.SetSlider("Crystalline_TimeCrystal_StopTimer",
                    state.StopStage <= 1 ? state.StopTimer : 0);
                level.Session.SetFlag("CrystallineHelper_TimeIsFrozen", state.StopStage == 1);
            }

            original(level);
        }

        private static void EndLevel(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            if (runtimes.TryGetValue(level.EngineContext, out var runtime)) runtime.State.Reset(false);
            original(level);
        }

        private static PlayerDeathPresentation PlayerDie(
            Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation> original,
            RoomPlayerSession player, Vector2 direction, bool invincible, bool register)
        {
            var result = original(player, direction, invincible, register);
            if (result != null && runtimes.TryGetValue(player.EngineContext, out var runtime))
                runtime.State.Reset(true);
            return result;
        }

        private static bool WallBoosterCheck(Func<RoomPlayerWorld, bool> original, RoomPlayerWorld world) =>
            (!runtimes.TryGetValue(world.EngineContext, out var runtime) || runtime.State.StopStage != 1) &&
            original(world);

        private static void CoreToggle(Action<OriginalCoreModeToggleState, RoomPlayerSession> original,
            OriginalCoreModeToggleState toggle, RoomPlayerSession player)
        {
            if (toggle.Scene is OriginalLevelSceneRoot level && runtimes.TryGetValue(level.EngineContext,
                                                                 out var runtime)
                                                             && runtime.State.StopStage == 1)
                toggle.Add(SourceAlarmState.Create(level.EngineContext.AlarmPool, SourceAlarmState.AlarmMode.Oneshot,
                    () => original(toggle, player), .01f, true));
            else original(toggle, player);
        }

        private static void LightningUpdate(Action<LightningRendererState> original, LightningRendererState lightning)
        {
            if (lightning.Scene is OriginalLevelSceneRoot level && runtimes.TryGetValue(level.EngineContext,
                                                                    out var runtime)
                                                                && runtime.State.StopStage == 1 &&
                                                                Array.IndexOf(runtime.State.EntityTypesToIgnore,
                                                                    "Celeste.LightningRenderer") < 0)
            {
                if (lightning.Dirty) lightning.RebuildEdges();
                lightning.ToggleEdges(true);
                lightning.Get<CustomBloomCallbackComponentState>().Update();
            }
            else original(lightning);
        }

        private static void SpinnerHue(Action<OriginalCrystalStaticSpinner> original,
            OriginalCrystalStaticSpinner spinner)
        {
            if (spinner.Scene is not OriginalLevelSceneRoot level || !runtimes.TryGetValue(level.EngineContext,
                                                                      out var runtime)
                                                                  || runtime.State.StopStage != 2) original(spinner);
        }

        private static void UpdateEntity(ISourceEntityUpdate update, ISourceEngineContext engine)
        {
            if (!runtimes.TryGetValue(engine, out var runtime) || !runtime.State.UseScaledDelta
                                                               || update is not SceneEntityState entity ||
                                                               entity.Scene is not OriginalLevelSceneRoot
                                                               || runtime.IsImmune(entity.GetType()) ||
                                                               runtime.IsIgnored(entity.GetType())
                                                               || runtime.State.ScaledDelta < 0 &&
                                                               entity is ParticleSystemPresentation)
            {
                update.Update();
                return;
            }

            float delta = engine.Frame.DeltaTime, raw = engine.Frame.RawDeltaTime;
            engine.Frame.DeltaTime = runtime.State.ScaledDelta;
            foreach (var component in entity.Components)
                if (component is SoundSourceComponentState sound)
                {
                    if (runtime.State.ScaledDelta == 0 && sound.Playing) sound.Pause();
                    else if (runtime.State.ScaledDelta != 0 && !sound.Playing) sound.Resume();
                }

            if (entity is ParticleSystemPresentation) engine.Frame.RawDeltaTime = runtime.State.ScaledRawDelta;
            try
            {
                update.Update();
            }
            finally
            {
                engine.Frame.DeltaTime = delta;
                engine.Frame.RawDeltaTime = raw;
            }
        }

        private static void UpdateRenderer(ISceneRenderer renderer, object scene)
        {
            if (scene is not OriginalLevelSceneRoot level || !runtimes.TryGetValue(level.EngineContext, out var runtime)
                                                          || !runtime.State.UseScaledDelta ||
                                                          runtime.Matches(renderer.GetType(),
                                                              "Celeste.DisplacementRenderer"))
            {
                renderer.Update(scene);
                return;
            }

            var frame = level.EngineContext.Frame;
            float delta = frame.DeltaTime;
            frame.DeltaTime = runtime.State.ScaledDelta;
            try
            {
                renderer.Update(scene);
            }
            finally
            {
                frame.DeltaTime = delta;
            }
        }

        private bool IsImmune(Type type)
        {
            if (immuneTypes.TryGetValue(type, out bool immune)) return immune;
            immune = typeof(TimeCrystal).IsAssignableFrom(type);
            string[] names =
            {
                "Celeste.Player", "Celeste.PlayerDeadBody", "Celeste.CrystalStaticSpinner", "Celeste.DustStaticSpinner",
                "Celeste.Lookout", "Celeste.FakeWall", "Celeste.GameplayStats", "FrostHelper.CustomSpinner",
                "VivHelper.Entities.CustomSpinner", "VivHelper.Entities.AnimatedSpinner"
            };
            for (var candidate = type; !immune && candidate != null; candidate = candidate.BaseType)
                foreach (string name in names)
                    if (Matches(candidate, name))
                    {
                        immune = true;
                        break;
                    }

            immuneTypes[type] = immune;
            return immune;
        }

        private bool IsIgnored(Type type)
        {
            if (!ReferenceEquals(ignoreSnapshot, State.EntityTypesToIgnore))
            {
                ignoreSnapshot = State.EntityTypesToIgnore;
                ignoredTypes.Clear();
            }

            if (ignoredTypes.TryGetValue(type, out bool ignored)) return ignored;
            ignored = false;
            foreach (string name in ignoreSnapshot)
                if (Matches(type, name, true))
                {
                    ignored = true;
                    break;
                }

            ignoredTypes[type] = ignored;
            return ignored;
        }

        private bool Matches(Type type, string name, bool shortName = false) =>
            ((ISourceEverestReflectionEnvironment)engine).Reflection.GetFakeEntryAssembly()
            .NameMatches(type, name, shortName);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            runtimes.Remove(engine);
            if (--users != 0) return;
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
