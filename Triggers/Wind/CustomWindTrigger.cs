using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Entities.Environment.Wind;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.Crystalline.Controllers.Wind;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.Crystalline.Triggers.Wind
{
    [CustomEntity("vitellary/customwindtrigger")]
    [RuntimeTypeAlias("vitmod.CustomWindTrigger")]
    public sealed class CustomWindTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly List<float> speedX = new();
        private readonly List<float> speedY = new();
        private readonly List<float> alternateSpeed = new();
        private readonly float catchupSpeed;
        private readonly string activationType;
        private readonly bool loop;
        private readonly bool persist;
        private readonly bool oneUse;
        private readonly int id;
        private readonly bool onRoomEnter;
        private readonly bool fixUpdateDepth;

        public CustomWindTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            foreach (string value in data.Attr("speedX", "0").Split(',')) speedX.Add(float.Parse(value));
            foreach (string value in data.Attr("speedY", "0").Split(',')) speedY.Add(float.Parse(value));
            foreach (string value in data.Attr("alternationSpeed", "0").Split(','))
                alternateSpeed.Add(float.Parse(value));
            catchupSpeed = data.Float("catchupSpeed", 1f);
            activationType = data.Attr("activationType");
            loop = data.Bool("loop", true);
            persist = data.Bool("persist");
            oneUse = data.Bool("oneUse");
            id = data.ID;
            onRoomEnter = data.Bool("onRoomEnter");
            fixUpdateDepth = data.Bool("fixUpdateDepth");
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (onRoomEnter)
                OnEnter(
                    ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>());
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            var scheduler = ((ISourceScene)Scene).SourceScene;
            RemoveFirst<OriginalWindController>();
            RemoveFirst<CustomWindController>();
            scheduler.AddEntity(new CustomWindController(engine, speedX, speedY, alternateSpeed,
                catchupSpeed, activationType, loop, persist, fixUpdateDepth));
            if (!oneUse) return;
            RemoveSelf();
            if (persist)
            {
                var session = ((OriginalLevelSceneRoot)Scene).Session;
                session.DoNotLoad.Add(new MapEntityId(session.Level, id));
            }
        }

        private void RemoveFirst<T>() where T : SceneEntityState
        {
            foreach (object entity in ((ISourceScene)Scene).SourceScene.Entities.Entities)
                if (entity is T found)
                {
                    found.RemoveSelf();
                    return;
                }
        }
    }
}
