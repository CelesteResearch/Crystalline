// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.Crystalline.Reflection;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.Crystalline.Modules;
using System.Linq;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Environment.Core;
using Celeste.Simulation.Gameplay.Holdables;

namespace Celeste.Mods.Crystalline.Triggers
{
    public sealed partial class TriggerTrigger
    {
        private readonly ActivationType activationType;
        private ComparisonType comparisonType;
        private bool absoluteValue;
        private int dashCount, deaths, playerState, collideCount;
        internal bool IncludeWalljump { get; private set; }
        internal bool ResetAfterJump { get; private set; }
        internal ActivationType Mode => activationType;
        private float requiredSpeed, waitTime, hasWaited;
        private RoomCoreMode coreMode;
        private HashSet<Type> collideTypes, solidTypes;
        private bool anySolid, ifSafe, includeCoyote;
        private readonly List<SceneEntityState> entitiesInside = new();

        private void InitializeConditions(EntityData data)
        {
            IncludeWalljump = data.Bool("includeWallJump");
            ResetAfterJump = data.Bool("resetAfterJump");
            collideCount = data.Int("collideCount", 1);
            comparisonType = data.Enum("comparisonType", ComparisonType.EqualTo);
            absoluteValue = data.Bool("absoluteValue");
            dashCount = data.Int("dashCount");
            deaths = data.Int("deaths", -1);
            playerState = data.Int("playerState");
            requiredSpeed = data.Float("requiredSpeed");
            waitTime = data.Float("timeToWait");
            coreMode = data.Enum("coreMode", RoomCoreMode.None);
            ifSafe = data.Bool("onlyIfSafe");
            includeCoyote = data.Bool("includeCoyote");
            if (activationType is ActivationType.OnEntityEnter or ActivationType.OnEntityCollide)
            {
                string type = data.Attr("entityType");
                if (string.IsNullOrEmpty(type)) type = data.Attr("entityTypeToCollide", "Celeste.Strawberry");
                collideTypes = EntityTypeList.ParseSingle(engine, type);
            }

            if (activationType == ActivationType.OnEntityCollide)
                ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CrystallineModule>().Single().TriggerHooks
                    .RegisterColliderController();
            if (activationType == ActivationType.OnSolid)
            {
                string type = data.Attr("solidType");
                anySolid = string.IsNullOrEmpty(type);
                if (!anySolid) solidTypes = EntityTypeList.ParseSingle(engine, type);
            }

            if (activationType == ActivationType.OnInteraction) InitializeInteraction(data);
            Add(new HoldableColliderState(holdable =>
            {
                if (activationType == ActivationType.OnHoldableEnter)
                    entitiesInside.Add((SceneEntityState)holdable.Entity);
            }));
        }

        private void UpdateConditions(RoomPlayerSession player)
        {
            if (player.Data.Motion.Normal.Speed == Vector2.Zero) hasWaited += engine.Frame.DeltaTime;
            else hasWaited = 0f;
            for (int index = entitiesInside.Count - 1; index >= 0; index--)
                if (!SourceEntityCollision.Check(entitiesInside[index], this))
                    entitiesInside.RemoveAt(index);
        }

        public bool GetActivateCondition(RoomPlayerSession player)
        {
            var level = (OriginalLevelSceneRoot)Scene;
            bool result = activationType switch
            {
                ActivationType.Flag => string.IsNullOrEmpty(flag) || level.Session.GetFlag(flag),
                ActivationType.Dashing => player.DashAttacking,
                ActivationType.DashCount => Compare(player.Data.Dashes, dashCount),
                ActivationType.DeathsInRoom => Compare(level.Session.DeathsInCurrentLevel, deaths),
                ActivationType.DeathsInLevel => Compare(level.Session.Deaths, deaths),
                ActivationType.OnHoldableEnter => entitiesInside.Count > 0,
                ActivationType.GrabHoldable => player.Holding.Holding != null && player.Holding.Holding.IsHeld,
                ActivationType.SpeedX => Compare(player.Data.Motion.Normal.Speed.X, requiredSpeed),
                ActivationType.SpeedY => Compare(player.Data.Motion.Normal.Speed.Y, requiredSpeed),
                ActivationType.Jumping => player.Data.AutoJumpTimer > 0f,
                ActivationType.Crouching => player.Data.Motion.Ducking,
                ActivationType.TimeSinceMovement => Compare(hasWaited, waitTime),
                ActivationType.CoreMode => ((OriginalLevelSceneRoot)player.Scene).CoreMode == coreMode,
                ActivationType.OnEntityCollide => Compare(
                    ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CrystallineModule>().Single().TriggerHooks
                    .GetCollideCount(collideTypes), collideCount),
                ActivationType.OnEntityEnter => HasEntityInside(),
                ActivationType.OnSolid => IsOnSolid(player),
                ActivationType.OnGrounded => IsGrounded(player),
                ActivationType.OnInteraction => false,
                ActivationType.OnPlayerState => player.Machine.State == playerState,
                _ => throw new NotSupportedException("The original activation mode is not ported.")
            };
            if (ExternalActivation) result = true;
            return invertCondition ? !result : result;
        }

        private bool HasEntityInside()
        {
            foreach (SceneEntityState entity in ((ISourceScene)Scene).SourceScene.Entities)
                if (SourceEntityCollision.Check(entity, this) && collideTypes.Contains(entity.GetType()))
                    return true;
            return false;
        }

        private bool IsOnSolid(RoomPlayerSession player)
        {
            var bounds = player.Collider.Bounds;
            bounds.Inflate(1, 3);
            foreach (ISourceEntityCollider solid in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         "Celeste.Solid"))
                if (solid.Collidable && SourceEntityGeometryCollision.Rectangle(solid, bounds)
                                     && (anySolid || solidTypes.Contains(solid.GetType())) && player.IsRiding(solid))
                    return true;
            return anySolid && player.OnGround();
        }

        private bool IsGrounded(RoomPlayerSession player)
        {
            bool result = player.OnGround();
            if (ifSafe) result &= player.Data.OnSafeGround;
            if (includeCoyote) result |= player.Data.Motion.JumpGraceTimer > 0f;
            return result;
        }

        private bool Compare(float first, float second)
        {
            if (absoluteValue)
            {
                first = Math.Abs(first);
                second = Math.Abs(second);
            }

            return comparisonType switch
            {
                ComparisonType.LessThan => first < second,
                ComparisonType.GreaterThan => first > second,
                _ => first == second
            };
        }
    }
}
