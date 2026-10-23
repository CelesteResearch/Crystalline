using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Reflection;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Scene.Entities;

namespace Celeste.Mods.Crystalline.Reflection
{
    internal sealed class EntityTypeListCache
    {
        private static readonly ConditionalWeakTable<SourceEverestReflectionProcessState, EntityTypeListCache> caches =
            new();

        private Entry current;

        internal static Entry For(SourceEverestReflectionProcessState reflection, SourceRuntimeTypeDiscovery discovery,
            SourceEverestModulesProcess modules)
        {
            var cache = caches.GetValue(reflection, static _ => new());
            long sourceRevision = reflection.SourceMembers.ActualTypeBindingsRevision;
            long runtimeRevision = discovery.Revision;
            var entry = cache.current;
            if (entry != null && entry.SourceRevision == sourceRevision && entry.RuntimeRevision == runtimeRevision
                && MatchesModules(entry.Modules, modules))
                return entry;

            var moduleSnapshot = modules.Modules.ToArray();
            var types = reflection.GetFakeEntryAssembly().GetTypes()
                .Where(type => type.IsSubclassOf(typeof(SceneEntityState))).ToArray();
            cache.current = new Entry(sourceRevision, runtimeRevision, moduleSnapshot, types);
            return cache.current;
        }

        private static bool MatchesModules(SourceEverestModule[] snapshot, SourceEverestModulesProcess modules)
        {
            var current = modules.Modules;
            if (snapshot.Length != current.Count) return false;
            for (int i = 0; i < snapshot.Length; i++)
                if (!ReferenceEquals(snapshot[i], current[i]))
                    return false;
            return true;
        }

        internal sealed class Entry
        {
            internal readonly long SourceRevision;
            internal readonly long RuntimeRevision;
            internal readonly SourceEverestModule[] Modules;
            internal readonly Type[] EntityTypes;
            private readonly string[] actualFullNames;
            private readonly string[] actualShortNames;
            private readonly string[] sourceFullNames;
            private readonly string[] sourceShortNames;
            private readonly bool[] sourceNamesRead;

            internal Entry(long sourceRevision, long runtimeRevision, SourceEverestModule[] modules, Type[] entityTypes)
            {
                SourceRevision = sourceRevision;
                RuntimeRevision = runtimeRevision;
                Modules = modules;
                EntityTypes = entityTypes;
                actualFullNames = new string[entityTypes.Length];
                actualShortNames = new string[entityTypes.Length];
                sourceFullNames = new string[entityTypes.Length];
                sourceShortNames = new string[entityTypes.Length];
                sourceNamesRead = new bool[entityTypes.Length];
                for (int i = 0; i < entityTypes.Length; i++)
                {
                    var type = entityTypes[i];
                    actualFullNames[i] = type.FullName;
                    actualShortNames[i] = type.Name;
                }
            }

            internal bool NameMatches(int index, string name, SourceEverestReflectionProcessState reflection)
            {
                if (name == null) return false;
                if (string.Equals(actualFullNames[index], name, StringComparison.Ordinal)
                    || string.Equals(actualShortNames[index], name, StringComparison.Ordinal)) return true;
                if (!sourceNamesRead[index])
                {
                    var members = reflection.SourceMembers;
                    if (members.ContainsSourceType(EntityTypes[index]))
                    {
                        string logicalName = members.GetOriginalTypeFullName(EntityTypes[index]);
                        sourceFullNames[index] = logicalName;
                        int separator = Math.Max(logicalName.LastIndexOf('.'), logicalName.LastIndexOf('+'));
                        sourceShortNames[index] = logicalName.Substring(separator + 1);
                    }

                    sourceNamesRead[index] = true;
                }

                return string.Equals(sourceFullNames[index], name, StringComparison.Ordinal)
                       || string.Equals(sourceShortNames[index], name, StringComparison.Ordinal)
                       || reflection.RuntimeTypeNameMatches(EntityTypes[index], name, allowShortName: true);
            }
        }
    }
}
