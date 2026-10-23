// SPDX-License-Identifier: MIT

using Celeste.Everest.Modules.Settings;

namespace Celeste.Mods.Crystalline.Configuration
{
    public sealed class CrystallineSettings : EverestModuleSettings
    {
        public RandomizationType RandomizationType { get; set; } = RandomizationType.FileTimer;
        public bool DisplayDashSequence { get; set; }
    }
}
