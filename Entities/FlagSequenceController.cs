// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.Crystalline.Modules;
using Celeste.Scene.Entities;

namespace Celeste.Mods.Crystalline.Entities
{
    [CustomEntity("vitellary/flagsequencecontroller")]
    [RuntimeTypeAlias("vitmod.FlagSequenceController")]
    public sealed class FlagSequenceController : SceneEntityState
    {
        public FlagSequenceController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            var scene = engine.Frame.Scenes.CurrentScene;
            var level = scene as OriginalLevelSceneRoot ?? (scene as OriginalLevelLoaderScene)?.Level;
            if (level == null || data.Bool("onlyOnRespawn") && !((ISourceEverestModulesEnvironment)engine).Modules
                    .OfType<CrystallineModule>().Single().TriggerHooks.Respawning) return;
            string prefix = data.Attr("prefix");
            if (string.IsNullOrEmpty(prefix)) return;
            bool state = data.Bool("state");
            for (int index = Math.Min(data.Int("startNumber", 1), 0); index <= data.Int("endNumber", 2); index++)
                level.Session.SetFlag(prefix + (index < 10 ? "_0" : "_") + index, state);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            RemoveSelf();
        }
    }
}
