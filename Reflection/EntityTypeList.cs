// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Reflection;
using Celeste.Everest.Reflection.Runtime;

namespace Celeste.Mods.Crystalline.Reflection
{
    internal static class EntityTypeList
    {
        public static HashSet<Type> Parse(ISourceEngineContext engine, string list)
        {
            return ParseNames(engine, list.Split(','));
        }

        public static HashSet<Type> ParseSingle(ISourceEngineContext engine, string name)
            => ParseNames(engine, new[] { name });

        private static HashSet<Type> ParseNames(ISourceEngineContext engine, string[] names)
        {
            var reflection = ((ISourceEverestReflectionEnvironment)engine).Reflection;
            var discovery = ((ISourceRuntimeTypeDiscoveryEnvironment)engine).RuntimeTypeDiscovery;
            var modules = ((ISourceEverestModulesProcessEnvironment)engine).ModulesProcess;
            var entry = EntityTypeListCache.For(reflection, discovery, modules);
            var result = new HashSet<Type>();
            for (int i = 0; i < entry.EntityTypes.Length; i++)
                foreach (string name in names)
                    if (entry.NameMatches(i, name, reflection))
                    {
                        result.Add(entry.EntityTypes[i]);
                        break;
                    }

            return result;
        }
    }
}
