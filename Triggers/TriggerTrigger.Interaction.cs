// SPDX-License-Identifier: MIT

using System.Collections;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Components.Talking;

namespace Celeste.Mods.Crystalline.Triggers
{
    public sealed partial class TriggerTrigger
    {
        private OriginalTalkComponent talker;

        private void InitializeInteraction(EntityData data)
        {
            Add(talker = new OriginalTalkComponent((IOriginalTalkProcess)engine,
                new IntRect(0, 0, (int)Width, (int)Height),
                new Vector2(data.Int("talkBubbleX", (int)Width / 2), data.Int("talkBubbleY")),
                _ => ExternalActivation = true) { PlayerMustBeFacing = false });
        }

        private void TryRemove()
        {
            if (activationType == ActivationType.OnInteraction)
            {
                Collidable = false;
                Add(new SimulationCoroutine(engine, InteractExit()));
            }
            else RemoveSelf();
        }

        private IEnumerator InteractExit()
        {
            if (talker == null)
            {
                RemoveSelf();
                yield break;
            }

            talker.Enabled = false;
            while (talker.UI.Slide > 0) yield return null;
            RemoveSelf();
        }
    }
}
