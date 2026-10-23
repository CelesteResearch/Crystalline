// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.Crystalline.Reflection;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.Crystalline.Triggers
{
    [CustomEntity("vitellary/editdepthtrigger")]
    [RuntimeTypeAlias("vitmod.EditDepthTrigger")]
    public sealed class EditDepthTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly HashSet<Type> affectedTypes;
        private readonly List<SceneEntityState> validEntitiesCache;
        private readonly int newDepth;
        private readonly bool debug;
        private readonly bool update;

        public EditDepthTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            newDepth = data.Int("depth");
            affectedTypes = EntityTypeList.Parse(engine, data.Attr("entitiesToAffect"));
            debug = data.Bool("debug");
            update = data.Bool("updateOnEntry");
            if (update && data.Bool("cacheValidEntities")) validEntitiesCache = new List<SceneEntityState>();
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            foreach (var actor in ((ISourceScene)scene).SourceScene.Entities)
            {
                var entity = (SceneEntityState)actor;
                HandleEntity(entity, fillCache: true);
                if (debug && SourceEntityCollision.Check(entity, this))
                    ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Info,
                        "CrystallineHelper/EditDepthTrigger", entity.GetType().FullName + ": " + entity.Depth);
            }
        }

        public override void Update()
        {
            base.Update();
            if (!update) return;
            if (validEntitiesCache != null)
            {
                foreach (var entity in validEntitiesCache) HandleEntity(entity, fillCache: false);
                return;
            }

            foreach (var entity in ((ISourceScene)Scene).SourceScene.Entities)
                HandleEntity((SceneEntityState)entity, fillCache: false);
        }

        private void HandleEntity(SceneEntityState entity, bool fillCache)
        {
            if (!affectedTypes.Contains(entity.GetType())) return;
            if (fillCache) validEntitiesCache?.Add(entity);
            if (SourceEntityCollision.Check(entity, this)) entity.Depth = newDepth;
        }
    }
}
