// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Mods.Crystalline.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime
{
    public sealed class KaizoBlockHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly Hook playerUpdate;

        public KaizoBlockHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            playerUpdate = new Hook(typeof(RoomPlayerSession).GetMethod("Update"),
                (Action<Action<RoomPlayerSession>, RoomPlayerSession>)UpdatePlayer);
        }

        private void UpdatePlayer(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (player.Scene is not Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level ||
                !ReferenceEquals(level.EngineContext, engine))
            {
                original(player);
                return;
            }

            var tracked = ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(typeof(KaizoBlock));
            if (tracked.Count == 0)
            {
                original(player);
                return;
            }

            var blocks = tracked.Cast<KaizoBlock>().Where(block => !block.Activated).ToList();
            foreach (var block in blocks) block.Collidable = block.Bottom < player.Top;
            original(player);
            foreach (var block in blocks) block.Collidable = false;
        }

        public void Dispose() => playerUpdate.Dispose();
    }
}
