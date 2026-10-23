// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Xml;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Content;
using Celeste.Core.Scheduling;
using Celeste.Mods.Crystalline.Configuration;
using Celeste.Mods.Crystalline.Runtime;
using Celeste.Mods.Crystalline.Runtime.Backdrops.Snow;
using Celeste.Mods.Crystalline.Runtime.MoveBlocks;
using Celeste.Everest.Modules.Runtime;

namespace Celeste.Mods.Crystalline.Modules
{
    public sealed class CrystallineModule : SourceEverestModule
    {
        private ForceDashCrystalHooks forceDashCrystals;
        private VitMoveBlockControllerHooks moveBlocks;
        private CustomWindSnowRegistration snow;
        private TimeCrystalRuntime timeCrystals;
        private ActionRestrictionHooks actionRestrictions;
        private CustomWindHooks customWind;
        private KaizoBlockHooks kaizoBlocks;
        public SpriteBank Sprites { get; private set; }

        public CrystallineModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public TriggerControllerHooks TriggerHooks { get; private set; }

        public override Type SettingsType => typeof(CrystallineSettings);
        public CrystallineSettings Settings => (CrystallineSettings)_Settings;

        public override void Load()
        {
            forceDashCrystals = new ForceDashCrystalHooks(SourceEngine);
            moveBlocks = new VitMoveBlockControllerHooks(SourceEngine);
            actionRestrictions = new ActionRestrictionHooks(SourceEngine);
            actionRestrictions.Load();
            TriggerHooks = new TriggerControllerHooks(SourceEngine);
            TriggerHooks.Load();
            timeCrystals = new TimeCrystalRuntime(SourceEngine);
            customWind = new CustomWindHooks(SourceEngine);
            customWind.Load();
            kaizoBlocks = new KaizoBlockHooks(SourceEngine);
            snow = new CustomWindSnowRegistration(SourceEngine);
        }

        public override void LoadContent(bool firstLoad)
        {
            timeCrystals.LoadContent();
            var registry = ((IEverestContentRegistryEnvironment)SourceEngine).SourceContentRegistry;
            var xml = new XmlDocument();
            using (var stream = registry.Get(Metadata.Name + ":/Graphics/Sprites").Stream) xml.Load(stream);
            Sprites = new SpriteBank(SourceEngine,
                ((ISourceGameGraphicsEnvironment)SourceEngine).GameGraphics.GameAtlas, xml);
        }

        public override void Unload()
        {
            forceDashCrystals?.Dispose();
            forceDashCrystals = null;
            kaizoBlocks?.Dispose();
            kaizoBlocks = null;
            customWind?.Dispose();
            customWind = null;
            Sprites = null;
            timeCrystals?.Dispose();
            timeCrystals = null;
            TriggerHooks?.Dispose();
            TriggerHooks = null;
            actionRestrictions?.Dispose();
            actionRestrictions = null;
            moveBlocks?.Dispose();
            moveBlocks = null;
            snow?.Dispose();
            snow = null;
        }
    }
}
