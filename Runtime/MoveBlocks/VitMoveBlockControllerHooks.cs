// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Mods.Crystalline.Platforms;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.Crystalline.Runtime.MoveBlocks
{
    internal sealed class VitMoveBlockControllerHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly ILHook hook;

        internal VitMoveBlockControllerHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            var controller =
                typeof(MovingBlockState).GetMethod("ControllerCore", BindingFlags.Instance | BindingFlags.NonPublic);
            var iterator = controller.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            hook = new ILHook(
                iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Patch);
        }

        private bool Owned(MovingBlockState block) =>
            block is VitMoveBlock && ReferenceEquals(block.EngineContext, engine);

        private bool Protect(MovingBlockState block) => !Owned(block) ||
                                                        (block.Direction is Celeste.Simulation.Gameplay.Platforms
                                                                .MoveBlock.MoveBlockDirection.Left
                                                            or Celeste.Simulation.Gameplay.Platforms.MoveBlock
                                                                .MoveBlockDirection.Right
                                                            ? ((VitMoveBlock)block).HeldTopButton
                                                            : ((VitMoveBlock)block).HeldSideButton);

        private void Patch(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(MoveType.Before, i => i.MatchLdarg(0), i => i.MatchLdcR4(0f),
                    i => i.OpCode == OpCodes.Stfld && ((FieldReference)i.Operand).Name.StartsWith("<crash>")))
                throw new InvalidOperationException("Vit move block outer controller loop is missing.");
            var restart = c.DefineLabel();
            c.MarkLabel(restart);
            c.Index = 0;
            int protects = 0;
            while (c.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(MovingBlockState), "get_FirstPlayer")))
            {
                var read = c.DefineLabel();
                var done = c.DefineLabel();
                c.MoveAfterLabels();
                c.Emit(OpCodes.Dup);
                c.EmitDelegate<Func<MovingBlockState, bool>>(Protect);
                c.Emit(OpCodes.Brtrue, read);
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldnull);
                c.Emit(OpCodes.Br, done);
                c.MarkLabel(read);
                c.Index++;
                c.MarkLabel(done);
                protects++;
            }

            if (protects != 2) throw new InvalidOperationException("Vit steering player reads no longer match.");
            var owner = il.Body.Instructions.Select(i => i.Operand).OfType<FieldReference>().FirstOrDefault(f =>
                f.Name == "<>4__this" && f.FieldType.FullName == typeof(MovingBlockState).FullName);
            if (owner == null) throw new InvalidOperationException("Vit controller owner is missing.");
            c.Index = 0;
            int lifts = 0;
            while (c.TryGotoNext(MoveType.Before,
                       i => i.MatchCall(
                           typeof(Celeste.Simulation.Gameplay.Platforms.Foundation.SourcePlatformEntityState),
                           "set_LiftSpeed")))
            {
                var write = c.DefineLabel();
                var done = c.DefineLabel();
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldfld, owner);
                c.EmitDelegate<Func<MovingBlockState, bool>>(Owned);
                c.Emit(OpCodes.Brfalse, write);
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Br, done);
                c.MarkLabel(write);
                c.Index++;
                c.MarkLabel(done);
                lifts++;
            }

            if (lifts != 2) throw new InvalidOperationException("Vit combined lift writes no longer match.");
            c.Index = 0;
            if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(MovingBlockState), "IsReformBlocked")))
                throw new InvalidOperationException("Vit reform clearance is missing.");
            var normal = c.DefineLabel();
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Brfalse, normal);
            // The real iterator owner field is compiler-generated for this actual controller.
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld, owner);
            c.EmitDelegate<Func<MovingBlockState, bool>>(Owned);
            c.Emit(OpCodes.Brfalse, normal);
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Br, restart);
            c.MarkLabel(normal);
        }

        public void Dispose() => hook.Dispose();
    }
}
