// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.Crystalline.Triggers
{
    [CustomEntity("vitellary/timedfadetrigger")]
    [SceneTracked]
    [RuntimeTypeAlias("vitmod.TimeFadeTrigger")]
    public sealed class TimeFadeTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly Vector2[] nodes;
        private RoomPlayerSession playerInside;
        public float TotalTime { get; }
        public bool OneUse { get; }
        public SourceTriggerEntityState TriggerToFade { get; private set; }
        public float ActivatedTimer { get; private set; }

        public TimeFadeTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            nodes = data.NodesOffset(offset);
            TotalTime = data.Float("time", 1f);
            OneUse = data.Bool("oneUse", true);
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            BindTarget(((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities("Celeste.Trigger"));
        }

        private void BindTarget(IReadOnlyList<object> targets)
        {
            var node = nodes[0];
            SourceTriggerEntityState selected = null;
            foreach (SourceTriggerEntityState target in targets)
                if (target.Collidable && SourceEntityGeometryCollision.Point(target, node))
                {
                    selected = target;
                    break;
                }

            if (selected == null)
            {
                float nearest = float.MaxValue;
                foreach (SourceTriggerEntityState target in targets)
                {
                    float distance = Vector2.DistanceSquared(target.Position, node);
                    if (!(distance < nearest)) continue;
                    selected = target;
                    nearest = distance;
                }
            }

            if (selected != null)
            {
                selected.Collidable = false;
                TriggerToFade = selected;
            }
        }

        public override void Update()
        {
            base.Update();
            if (playerInside == null || !(ActivatedTimer < TotalTime)) return;
            ActivatedTimer += engine.Frame.DeltaTime;
            if (ActivatedTimer >= TotalTime)
            {
                ActivatedTimer = TotalTime;
                if (TriggerToFade != null)
                {
                    TriggerToFade.OnLeave(playerInside);
                    if (OneUse)
                    {
                        RemoveSelf();
                        return;
                    }

                    playerInside = null;
                    ActivatedTimer = 0f;
                }
            }
            else TriggerToFade?.OnStay(playerInside);
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            TriggerToFade?.OnEnter(player);
            playerInside = player;
        }
    }
}
