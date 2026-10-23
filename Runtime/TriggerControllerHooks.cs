// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Simulation.Gameplay.Triggers.Foundation;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Mods.Crystalline.Triggers;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class TriggerControllerHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();
        private readonly HashSet<SceneEntityState> collidedEntities = new();
        private readonly Dictionary<Type, int> collideTracker = new();
        private bool hasOnEntityCollide;
        public bool Respawning { get; private set; }

        public TriggerControllerHooks(ISourceEngineContext engine) => this.engine = engine;

        public void Load()
        {
            try
            {
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                    (Action<Action<OriginalLevelSceneRoot, PlayerIntroType, bool>, OriginalLevelSceneRoot,
                        PlayerIntroType,
                        bool>)LoadLevel));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Jump)),
                    (Action<Action<RoomPlayerSession, bool, bool>, RoomPlayerSession, bool, bool>)Jump));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.WallJump)),
                    (Action<Action<RoomPlayerSession, int>, RoomPlayerSession, int>)WallJump));
                hooks.Add(new Hook(
                    typeof(PlayerColliderComponentState).GetMethod(nameof(PlayerColliderComponentState.Check)),
                    (Func<Func<PlayerColliderComponentState, RoomPlayerSession, bool>, PlayerColliderComponentState,
                        RoomPlayerSession, bool>)ColliderCheck));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            collidedEntities.Clear();
            collideTracker.Clear();
            hasOnEntityCollide = false;
            Respawning = false;
        }

        private bool Owns(RoomPlayerSession player) => player?.Scene is OriginalLevelSceneRoot level &&
                                                       ReferenceEquals(level.EngineContext, engine);

        private void LoadLevel(Action<OriginalLevelSceneRoot, PlayerIntroType, bool> orig, OriginalLevelSceneRoot level,
            PlayerIntroType intro, bool fromLoader)
        {
            bool owns = ReferenceEquals(level.EngineContext, engine);
            if (owns)
            {
                hasOnEntityCollide = false;
                collidedEntities.Clear();
                collideTracker.Clear();
                Respawning = intro > PlayerIntroType.Transition;
            }

            try
            {
                orig(level, intro, fromLoader);
            }
            finally
            {
                if (owns) Respawning = false;
            }
        }

        private float PositionLerp(
            Func<SourceTriggerEntityState, RoomPlayerSession, SourceTriggerEntityState.PositionModes, float> orig,
            SourceTriggerEntityState self, RoomPlayerSession player, SourceTriggerEntityState.PositionModes mode)
        {
            if (self.Scene is OriginalLevelSceneRoot level && ReferenceEquals(level.EngineContext, engine))
                foreach (TimeFadeTrigger fade in ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(
                             typeof(TimeFadeTrigger)))
                    if (ReferenceEquals(fade.TriggerToFade, self))
                        return fade.ActivatedTimer / fade.TotalTime;
            return orig(self, player, mode);
        }

        public void RegisterColliderController() => hasOnEntityCollide = true;

        public int GetCollideCount(HashSet<Type> types)
        {
            int count = 0;
            foreach (Type type in types)
                if (collideTracker.TryGetValue(type, out int value))
                    count += value;
            return count;
        }

        private bool ColliderCheck(Func<PlayerColliderComponentState, RoomPlayerSession, bool> orig,
            PlayerColliderComponentState collider, RoomPlayerSession player)
        {
            bool result = orig(collider, player);
            if (hasOnEntityCollide && Owns(player)) RecordCollision((SceneEntityState)collider.Entity, result);
            return result;
        }

        internal void RecordCollision(SceneEntityState entity, bool result)
        {
            if (result)
            {
                if (collidedEntities.Add(entity))
                {
                    Type type = entity.GetType();
                    collideTracker.TryGetValue(type, out int count);
                    collideTracker[type] = count + 1;
                }
            }
            else collidedEntities.Remove(entity);
        }

        private void Jump(Action<RoomPlayerSession, bool, bool> orig, RoomPlayerSession player, bool particles,
            bool playSfx)
        {
            orig(player, particles, playSfx);
            if (Owns(player)) ActivateJump(player, false);
        }

        private void WallJump(Action<RoomPlayerSession, int> orig, RoomPlayerSession player, int direction)
        {
            orig(player, direction);
            if (Owns(player)) ActivateJump(player, true);
        }

        internal void ActivateJump(RoomPlayerSession player, bool walljump)
        {
            foreach (TriggerTrigger trigger in ((ISourceScene)player.Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(TriggerTrigger)))
            {
                if (trigger.Mode != ActivationType.Jumping || walljump && !trigger.IncludeWalljump) continue;
                trigger.ExternalActivation = true;
                if (trigger.ResetAfterJump) trigger.ResetActivation = true;
                else player.Add(new SimulationCoroutine(engine, JumpRoutine(player, trigger)));
            }
        }

        private static IEnumerator JumpRoutine(RoomPlayerSession player, TriggerTrigger trigger)
        {
            while (!player.OnGround()) yield return null;
            trigger.ExternalActivation = false;
        }
    }
}
