// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Game.Controls.Source;
using Celeste.Input.Gameplay;
using Celeste.Mods.Crystalline.Entities;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class ForceDashCrystalHooks : IDisposable
    {
        private delegate Vector2 AimOriginal(OriginalGameInputProcess input, int defaultFacing);

        private delegate Vector2 AimHook(AimOriginal original, OriginalGameInputProcess input, int defaultFacing);

        private readonly ISourceGameInput input;
        private readonly Hook hook;

        public ForceDashCrystalHooks(ISourceEngineContext context)
        {
            input = ((ISourceGameInputEnvironment)context).Input;
            hook = new Hook(typeof(OriginalGameInputProcess).GetMethod(nameof(OriginalGameInputProcess.GetAimVector)),
                (AimHook)GetAimVector);
        }

        private Vector2 GetAimVector(AimOriginal original, OriginalGameInputProcess current, int defaultFacing)
            => ReferenceEquals(input, current) && ForceDashCrystal.dirToUse is { } direction
                ? direction
                : original(current, defaultFacing);

        public void Dispose() => hook.Dispose();
    }
}
