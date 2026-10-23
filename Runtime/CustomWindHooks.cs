using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Environment.Wind;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Game.Gameplay.Holdables.Gliders;
using Celeste.Game.Gameplay.Holdables.TheoCrystal;
using Celeste.Mods.Crystalline.Controllers.Wind;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class CustomWindHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        public CustomWindHooks(ISourceEngineContext engine) => this.engine = engine;

        public void Load()
        {
            try
            {
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Reload)),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)Reload));
                hooks.Add(new Hook(typeof(OriginalWindController).GetMethod(nameof(OriginalWindController.Update)),
                    (Action<Action<OriginalWindController>, OriginalWindController>)WindUpdate));
                hooks.Add(new Hook(
                    typeof(OriginalRefillState).GetMethod("OnPlayer", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<OriginalRefillState, RoomPlayerSession>, OriginalRefillState, RoomPlayerSession>)
                    RefillOnPlayer));
                hooks.Add(new Hook(
                    typeof(OriginalGliderState).GetMethod("OnPickup", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<OriginalGliderState>, OriginalGliderState>)GliderOnPickup));
                hooks.Add(new Hook(
                    typeof(TheoCrystalState).GetMethod("OnPickup", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<TheoCrystalState>, TheoCrystalState>)TheoOnPickup));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private CustomWindController GetController(SceneEntityState entity)
        {
            if (entity.Scene is not OriginalLevelSceneRoot level ||
                !ReferenceEquals(level.EngineContext, engine)) return null;
            return level.SourceScene.Entities.GetTrackedEntity<CustomWindController>();
        }

        private void Reload(Action<OriginalLevelSceneRoot> orig, OriginalLevelSceneRoot level)
        {
            orig(level);
            if (!ReferenceEquals(level.EngineContext, engine) || level.Completed) return;
            CustomWindController custom = null;
            OriginalWindController vanilla = null;
            foreach (object entry in level.SourceScene.Entities.Entities)
            {
                if (entry is CustomWindController foundCustom && custom == null) custom = foundCustom;
                if (entry is OriginalWindController foundVanilla && vanilla == null) vanilla = foundVanilla;
            }

            if (custom != null) custom.SnapWind();
            else if (vanilla == null) level.Wind = System.Numerics.Vector2.Zero;
        }

        private void WindUpdate(Action<OriginalWindController> orig, OriginalWindController self)
        {
            if (GetController(self) == null) orig(self);
        }

        private void RefillOnPlayer(Action<OriginalRefillState, RoomPlayerSession> orig, OriginalRefillState self,
            RoomPlayerSession player)
        {
            orig(self, player);
            if (!self.Collidable) Activate(self, "Refill");
        }

        private void GliderOnPickup(Action<OriginalGliderState> orig, OriginalGliderState self)
        {
            orig(self);
            Activate(self, "Jellyfish");
        }

        private void TheoOnPickup(Action<TheoCrystalState> orig, TheoCrystalState self)
        {
            orig(self);
            Activate(self, "Theo");
        }

        private void Activate(SceneEntityState entity, string type)
        {
            CustomWindController controller = GetController(entity);
            if (controller?.ActivationType == type) controller.ActivateWind();
        }
    }
}
