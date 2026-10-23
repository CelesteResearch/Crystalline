// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Timing;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.Crystalline.Triggers
{
    [CustomEntity("vitellary/triggertrigger")]
    [Celeste.Scene.Tracking.Attributes.SceneTracked]
    [RuntimeTypeAlias("vitmod.TriggerTrigger")]
    public sealed partial class TriggerTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly Vector2[] nodes;
        private readonly bool oneUse;
        private readonly string flag;
        private readonly bool invertCondition;
        private readonly float delay;
        private readonly bool matchPosition;
        private readonly bool onlyOnEnter;
        private readonly List<SourceTriggerEntityState> triggers = new();
        private bool activating;
        private bool deactivating;

        public bool Global { get; }
        public bool Activated { get; private set; }
        public bool ExternalActivation { get; set; }
        public bool ResetActivation { get; set; }

        public TriggerTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            activationType = data.Enum("activationType", ActivationType.Flag);
            if (activationType == ActivationType.OnInput)
                throw new NotSupportedException("The original input-mode freeze integration is not ported yet.");
            InitializeConditions(data);
            randomize = data.Bool("randomize");
            nodes = data.NodesOffset(offset);
            oneUse = data.Bool("oneUse");
            invertCondition = data.Has("invertFlag") ? data.Bool("invertFlag") : data.Bool("invertCondition");
            flag = data.Attr("flag");
            delay = data.Float("delay");
            Global = data.Bool("activateOnTransition");
            if (data.Has("requirePlayerInside")) Global = !data.Bool("requirePlayerInside", true);
            if (activationType is ActivationType.OnHoldableEnter or ActivationType.OnInteraction
                or ActivationType.OnEntityEnter)
                Global = true;
            matchPosition = data.Bool("matchPosition", true);
            onlyOnEnter = data.Bool("onlyOnEnter");
            Add(new RoomTransitionListener { OnOut = _ => DeactivateTriggers(FindPlayer()) });
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            BindTargets(((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities("Celeste.Trigger"));
        }

        private void BindTargets(IReadOnlyList<object> tracked)
        {
            foreach (var node in nodes)
            {
                var collidable = new Dictionary<SourceTriggerEntityState, bool>();
                foreach (SourceTriggerEntityState trigger in tracked)
                {
                    collidable.Add(trigger, trigger.Collidable);
                    trigger.Collidable = true;
                }

                SourceTriggerEntityState selected = null;
                foreach (SourceTriggerEntityState trigger in tracked)
                    if (SourceEntityGeometryCollision.Point(trigger, node))
                    {
                        selected = trigger;
                        break;
                    }

                foreach (SourceTriggerEntityState trigger in tracked) trigger.Collidable = collidable[trigger];
                if (selected == null)
                {
                    float nearest = float.MaxValue;
                    foreach (SourceTriggerEntityState trigger in tracked)
                    {
                        float distance = Vector2.DistanceSquared(trigger.Position, node);
                        if (!(distance < nearest)) continue;
                        nearest = distance;
                        selected = trigger;
                    }
                }

                if (selected == null || ReferenceEquals(selected, this)) continue;
                triggers.Add(selected);
                selected.Collidable = false;
            }
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            if (Global) return;
            TryActivate(player);
            if (Activated && oneUse) TryRemove();
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);
            if (!Global) TryDeactivate(player, inside: false);
        }

        public override void Update()
        {
            base.Update();
            var player = FindPlayer();
            if (player == null) return;
            UpdateConditions(player);
            if (!onlyOnEnter)
            {
                if (Global || PlayerIsInside)
                {
                    TryActivate(player);
                    TryDeactivate(player, inside: true);
                }
                else TryDeactivate(player, inside: false);
            }

            if (Activated)
            {
                if (oneUse) TryRemove();
                else UpdateTriggers(player);
            }

            if (ResetActivation)
            {
                ExternalActivation = false;
                ResetActivation = false;
            }
        }

        private RoomPlayerSession FindPlayer()
            => Scene == null
                ? null
                : ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>();

        public void TryActivate(RoomPlayerSession player)
        {
            if (activating || Activated && !deactivating || !GetActivateCondition(player)) return;
            if (delay > 0f)
            {
                activating = true;
                Add(SourceAlarmState.Create(engine.AlarmPool, SourceAlarmState.AlarmMode.Oneshot, () =>
                {
                    activating = false;
                    ActivateTriggers(player);
                }, delay, true));
            }
            else ActivateTriggers(player);
        }

        public void TryDeactivate(RoomPlayerSession player, bool inside)
        {
            if (deactivating || !Activated && !activating || inside && GetActivateCondition(player)) return;
            if (delay > 0f)
            {
                deactivating = true;
                Add(SourceAlarmState.Create(engine.AlarmPool, SourceAlarmState.AlarmMode.Oneshot, () =>
                {
                    deactivating = false;
                    DeactivateTriggers(player);
                }, delay, true));
            }
            else DeactivateTriggers(player);
        }
    }
}
