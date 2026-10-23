// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Celeste.Core.Randomness;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Execution.Processes;
using Celeste.Mods.Crystalline.Configuration;
using Celeste.Mods.Crystalline.Modules;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation;

namespace Celeste.Mods.Crystalline.Triggers
{
    public sealed partial class TriggerTrigger
    {
        private readonly bool randomize;
        private Celeste.Simulation.Gameplay.Triggers.Foundation.SourceTriggerEntityState chosenTrigger;

        private void CleanTriggers()
        {
            triggers.RemoveAll(trigger => trigger.Scene == null);
            if (chosenTrigger?.Scene == null) chosenTrigger = null;
        }

        private int GetSeed()
        {
            var module = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CrystallineModule>().Single();
            long time = module.Settings.RandomizationType == RandomizationType.FileTimer
                ? ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Time
                : ((OriginalLevelSceneRoot)Scene).Session.Time;
            return (int)(time % int.MaxValue);
        }


        private void ActivateTriggers(RoomPlayerSession player)
        {
            DeactivateTriggers(player);
            CleanTriggers();
            Activated = true;
            if (!randomize)
            {
                foreach (var trigger in triggers)
                {
                    if (trigger.PlayerIsInside) trigger.OnLeave(player);
                    trigger.OnEnter(player);
                }
            }
            else if (triggers.Count > 0)
            {
                var random = ((ISourceRandomProcessEnvironment)engine).RandomProcess;
                random.PushRandom(GetSeed());
                chosenTrigger = triggers[random.Random.Next(triggers.Count)];
                random.PopRandom();
                if (chosenTrigger.PlayerIsInside) chosenTrigger.OnLeave(player);
                chosenTrigger.OnEnter(player);
            }
        }

        private void DeactivateTriggers(RoomPlayerSession player)
        {
            CleanTriggers();
            Activated = false;
            foreach (var trigger in triggers)
                if (trigger.PlayerIsInside)
                    trigger.OnLeave(player);
            chosenTrigger = null;
        }

        private void UpdateTriggers(RoomPlayerSession player)
        {
            CleanTriggers();
            foreach (var trigger in triggers)
            {
                if (matchPosition)
                {
                    if (!Global)
                    {
                        trigger.Position = Position;
                        trigger.Collider.Width = Width;
                        trigger.Collider.Height = Height;
                    }
                    else
                    {
                        var bounds = ((OriginalLevelSceneRoot)Scene).Bounds;
                        trigger.Position = new(bounds.X, bounds.Y);
                        trigger.Collider.Width = bounds.Width;
                        trigger.Collider.Height = bounds.Height;
                    }
                }

                if (!randomize) trigger.OnStay(player);
            }

            chosenTrigger?.OnStay(player);
        }
    }
}
