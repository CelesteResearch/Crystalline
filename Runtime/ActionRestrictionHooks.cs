// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Mods.Crystalline.Triggers;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.Movement;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class ActionRestrictionHooks : IDisposable
    {
        private static readonly Func<DashSequence, object> DashWorld = CreateWorldGetter<DashSequence>();
        private static readonly Func<ClimbSequence, object> ClimbWorld = CreateWorldGetter<ClimbSequence>();
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        public ActionRestrictionHooks(ISourceEngineContext engine) => this.engine = engine;

        public void Load()
        {
            if (hooks.Count != 0) return;
            try
            {
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Jump)),
                    (Action<Action<RoomPlayerSession, bool, bool>, RoomPlayerSession, bool, bool>)Jump));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.SuperJump)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)SuperJump));
                hooks.Add(new Hook(typeof(DashSequence).GetProperty(nameof(DashSequence.CanDash)).GetMethod,
                    (Func<Func<DashSequence, bool>, DashSequence, bool>)CanDash));
                hooks.Add(new Hook(typeof(ClimbSequence).GetMethod(nameof(ClimbSequence.Update)),
                    (Func<Func<ClimbSequence, int>, ClimbSequence, int>)ClimbUpdate));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            List<Exception> failures = null;
            for (int index = hooks.Count - 1; index >= 0; index--)
                try
                {
                    hooks[index].Dispose();
                }
                catch (Exception exception)
                {
                    (failures ??= new()).Add(exception);
                }

            hooks.Clear();
            if (failures != null) throw new AggregateException(failures);
        }

        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.World.EngineContext, engine);

        internal static bool Overlaps<T>(RoomPlayerSession player) where T : class, ISourceEntityCollider
        {
            if (player.Scene == null) return false;
            foreach (T trigger in ((ISourceScene)player.Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(T)))
                if (SourceEntityCollision.Check(player, trigger))
                    return true;
            return false;
        }

        private bool CanDash(Func<DashSequence, bool> orig, DashSequence sequence)
        {
            if (DashWorld(sequence) is not RoomPlayerWorld world ||
                world.Body.SourceColliderOwner is not RoomPlayerSession player)
                return orig(sequence);
            if (Owns(player) && !player.Dead && Overlaps<NoDashTrigger>(player)) return false;
            return orig(sequence);
        }

        private int ClimbUpdate(Func<ClimbSequence, int> orig, ClimbSequence sequence)
        {
            if (ClimbWorld(sequence) is not RoomPlayerWorld world ||
                world.Body.SourceColliderOwner is not RoomPlayerSession player)
                return orig(sequence);
            if (Owns(player) && !player.Dead && Overlaps<NoGrabTrigger>(player))
            {
                player.Speed += JumpMotion.LiftBoost(player.LiftSpeed);
                return 0;
            }

            return orig(sequence);
        }

        private void Jump(Action<RoomPlayerSession, bool, bool> orig, RoomPlayerSession player, bool particles,
            bool playSfx)
        {
            if (!Owns(player) || !Overlaps<NoJumpTrigger>(player) ||
                player.World.SolidAt(Vector2.UnitX * player.Facing) && player.Input.Grab.Check)
                orig(player, particles, playSfx);
        }

        private void SuperJump(Action<RoomPlayerSession> orig, RoomPlayerSession player)
        {
            if (!Owns(player) || !Overlaps<NoJumpTrigger>(player)) orig(player);
        }

        private static Func<T, object> CreateWorldGetter<T>()
        {
            var field = typeof(T).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException(typeof(T).FullName, "world");
            var sequence = Expression.Parameter(typeof(T), "sequence");
            return Expression.Lambda<Func<T, object>>(
                Expression.Convert(Expression.Field(sequence, field), typeof(object)), sequence).Compile();
        }
    }
}
