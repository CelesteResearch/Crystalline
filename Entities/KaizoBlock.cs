// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components.Timing;
using Celeste.Core.Maps;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Mods.Crystalline.Modules;
using Celeste.Physics.Collision;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Collision;

namespace Celeste.Mods.Crystalline.Entities
{
    [CustomEntity("vitellary/kaizoblock")]
    [RuntimeTypeAlias("vitmod.KaizoBlock")]
    [SceneTracked]
    public sealed class KaizoBlock : SourceSolidEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly bool persistent;
        private readonly float dashDisable;
        private readonly string id;
        private readonly Sprite sprite;
        private readonly WigglerComponent spriteWiggler;
        private readonly WigglerComponent moveWiggler;
        public bool Activated;

        public KaizoBlock(ISourceEngineContext engine, EntityData data, Vector2 offset, MapEntityId entityId,
            ISourceSolidWorld world, ISpriteFactory sprites)
            : base(data.Position + offset - new Vector2(8f, 8f), 16f, 16f, true, world)
        {
            this.engine = engine;
            persistent = data.Bool("persistent");
            dashDisable = data.Float("noDashTimer", .2f);
            Depth = -9999;
            id = entityId.ToString();
            Collidable = false;
            Add(sprite = sprites.Create("kaizoblock"));
            sprite.Play("idle");
            sprite.Visible = false;
            sprite.Position = new Vector2(8f, 8f);
            Add(spriteWiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(.5f, 4f,
                value => sprite.Scale = Vector2.One * (1f + value * .25f)));
            moveWiggler = ((ISourceWigglerPoolScope)engine).WigglerPool.Create(.8f, 2f);
            moveWiggler.StartZero = true;
            Add(moveWiggler);
        }

        public static KaizoBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
        {
            var engine = level.EngineContext;
            var position = data.Position + offset - new Vector2(8f, 8f);
            var world = ((ISourcePlatformCollisionScene)level).SolidWorldFactory.Create(position, true);
            var sprites = ((ISourceEverestModulesEnvironment)engine).Modules.OfType<CrystallineModule>().Single()
                .Sprites;
            return new KaizoBlock(engine, data, offset, new MapEntityId(room.Name, data.ID), world, sprites);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            var level = (OriginalLevelSceneRoot)scene;
            if (persistent && level.Session.GetFlag("kaizoblock_flag_" + id))
            {
                Activated = true;
                Collidable = true;
                sprite.Visible = true;
            }

            ((ISourceScene)scene).SourceScene.AddEntity(new KaizoBlockOutline(engine, this));
        }

        public override void Update()
        {
            base.Update();
            if (Activated) return;
            var registry = ((ISourceScene)Scene).SourceScene.Entities;
            var player = registry.GetTrackedEntities(typeof(RoomPlayerSession)).OfType<RoomPlayerSession>()
                .FirstOrDefault();
            if (player != null && SourceEntityCollision.CheckAt(this, player, Position + Vector2.UnitY) &&
                player.Data.Motion.Normal.Speed.Y < 0f)
                Hit(player);
        }

        private void Hit(RoomPlayerSession player)
        {
            player.Data.Motion.Normal.Speed.Y = 160f;
            player.Machine.State = 0;
            player.NaiveMove(new Vector2(0f, Bottom - player.Top));
            player.Data.CooldownTimer = dashDisable;
            var audio = ((ISourceSpatialAudioEnvironment)engine).SpatialAudio;
            audio.Play("event:/game/general/thing_booped", Position);
            sprite.Play("flash");
            sprite.Visible = true;
            Add(SourceAlarmState.Create(engine.AlarmPool, SourceAlarmState.AlarmMode.Oneshot,
                () => audio.Play("event:/game/07_summit/checkpoint_confetti", Position), .25f, true));
            spriteWiggler.Start();
            moveWiggler.Start();
            Activated = true;
            Collidable = true;
            if (persistent) ((OriginalLevelSceneRoot)Scene).Session.SetFlag("kaizoblock_flag_" + id);
        }
    }
}
