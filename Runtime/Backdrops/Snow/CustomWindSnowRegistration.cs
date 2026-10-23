// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Maps.Binary;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Formats.Map.Binary.Source;
using Celeste.Game.Content.Maps;
using Celeste.Mods.Crystalline.Backdrops.Weather.Snow;
using Celeste.Simulation.Presentation.Backdrops.Runtime;

namespace Celeste.Mods.Crystalline.Runtime.Backdrops.Snow
{
    public sealed class CustomWindSnowRegistration : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly EverestLevelUpdateEvents events;

        public CustomWindSnowRegistration(ISourceEngineContext engine)
        {
            this.engine = engine;
            events = ((IEverestLevelEntityLoadEnvironment)engine).LevelUpdateEvents;
            events.OnLoadBackdrop += LoadBackdrop;
        }

        private BackdropPresentation LoadBackdrop(OriginalChapterMapData map, BinaryPackerElement child,
            BinaryPackerElement above)
            => child.Name == "CrystallineHelper/CustomWindSnow"
                ? new CustomWindSnow(engine, child.Attr("colors", "ffffff"), child.Attr("alphas", "1"),
                    child.AttrInt("amount", 240), child.AttrFloat("speedX"), child.AttrFloat("speedY"),
                    child.AttrBool("ignoreWind"))
                : null;

        public void Dispose() => events.OnLoadBackdrop -= LoadBackdrop;
    }
}
