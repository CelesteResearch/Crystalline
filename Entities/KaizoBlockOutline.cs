// SPDX-License-Identifier: MIT

using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;

namespace Celeste.Mods.Crystalline.Entities
{
    [RuntimeTypeAlias("vitmod.KaizoBlock+OutlineRenderer")]
    public sealed class KaizoBlockOutline : SceneEntityState
    {
        private readonly KaizoBlock block;

        public KaizoBlockOutline(ISourceEngineContext engine, KaizoBlock block)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            Depth = 5000;
            this.block = block;
        }

        public override void Render(ISourceRenderContext context)
        {
            if (!block.Activated) return;
            SourcePrimitiveDrawing.Rect(context, this, (int)(block.X + block.Shake.X),
                (int)(block.Y + block.Shake.Y - 1f), (int)block.Width, (int)block.Height + 2, PackedColor.Black);
            SourcePrimitiveDrawing.Rect(context, this, (int)(block.X + block.Shake.X - 1f),
                (int)(block.Y + block.Shake.Y), (int)block.Width + 2, (int)block.Height, PackedColor.Black);
        }
    }
}
