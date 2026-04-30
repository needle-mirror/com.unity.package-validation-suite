using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;
using Unity.APIComparison.Framework.Changes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class APIChangesCollector : IDisposable
    {
        public struct AssemblyInfo
        {
            public string BaseAssemblyPath;
            public string CurrentAssemblyPath;
            public IEnumerable<string> BaseAssemblyExtraSearchFolders;
            public IEnumerable<string> CurrentExtraSearchFolders;

            public override string ToString()
            {
                return "Comparing: \n\t" + BaseAssemblyPath + "\n\t" + CurrentAssemblyPath;
            }
        }

        private AssemblyDefinition oldAssembly;
        private AssemblyDefinition newAssembly;

        public static IEnumerable<IEntityChange> Collect(string oldPath, string newPath)
        {
            return Collect(new AssemblyInfo
            {
                BaseAssemblyPath = oldPath,
                CurrentAssemblyPath = newPath
            });
        }

        public void Dispose()
        {
            oldAssembly.Dispose();
            newAssembly.Dispose();
        }

        public static IEnumerable<IEntityChange> Collect(AssemblyInfo assemblyInfo)
        {
            using (var collector = new APIChangesCollector(assemblyInfo))
                return collector.Collect();
        }

        private APIChangesCollector(AssemblyInfo assemblyInfo)
        {
            if (assemblyInfo.BaseAssemblyPath == null && assemblyInfo.CurrentAssemblyPath == null)
                throw new ArgumentException("Either assemblyInfo.BaseAssemblyPath or assemblyInfo.CurrentAssemblyPath must be non-null");

            var newAssemblyResolver = new DefaultAssemblyResolver();
            if (assemblyInfo.CurrentAssemblyPath != null)
                newAssemblyResolver.AddSearchDirectory(Path.GetDirectoryName(assemblyInfo.CurrentAssemblyPath));

            AddPathsToAssemblySearchDirectoryList(newAssemblyResolver, assemblyInfo.CurrentExtraSearchFolders);

            var oldAssemblyResolver = new DefaultAssemblyResolver();
            if (assemblyInfo.BaseAssemblyPath != null)
                oldAssemblyResolver.AddSearchDirectory(Path.GetDirectoryName(assemblyInfo.BaseAssemblyPath));

            AddPathsToAssemblySearchDirectoryList(oldAssemblyResolver, assemblyInfo.BaseAssemblyExtraSearchFolders);

            oldAssembly = ReadOrCreateAssembly(assemblyInfo.BaseAssemblyPath, oldAssemblyResolver);
            newAssembly = ReadOrCreateAssembly(assemblyInfo.CurrentAssemblyPath, newAssemblyResolver);
        }

        static AssemblyDefinition ReadOrCreateAssembly(string assemblyPath, IAssemblyResolver assemblyResolver)
        {
            if (assemblyPath == null)
                return AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Placeholder", new Version()), "PlaceholderModule", ModuleKind.Dll);

            return AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { AssemblyResolver = assemblyResolver, SymbolReaderProvider = HasSymbolAvailable(assemblyPath) });
        }

        static void AddPathsToAssemblySearchDirectoryList(DefaultAssemblyResolver resolver, IEnumerable<string> extraSearchFolders)
        {
            if (extraSearchFolders != null)
            {
                foreach (var path in extraSearchFolders)
                {
                    resolver.AddSearchDirectory(path);
                    foreach (var subfolder in Directory.GetDirectories(path, "*.*", SearchOption.AllDirectories))
                    {
                        resolver.AddSearchDirectory(subfolder);
                    }
                }
            }
        }

        static ISymbolReaderProvider HasSymbolAvailable(string assemblyPath)
        {
            if (File.Exists(Path.ChangeExtension(assemblyPath, ".pdb")))
                return new PdbReaderProvider();

            if (File.Exists(assemblyPath + ".mdb"))
                return new MdbReaderProvider();

            return null;
        }

        private IEnumerable<IEntityChange> Collect()
        {
            var changes = new Dictionary<string, IEntityChange>();
            foreach (var type in oldAssembly.Modules.SelectMany(m => m.Types).Where(IsNotCompilerGenerated))
            {
                ProcessType(type, changes);
            }

            CheckForNewTypes(oldAssembly, newAssembly, changes);

            return changes.Values;
        }

        private void CheckForNewTypes(AssemblyDefinition originalAssembly, AssemblyDefinition currentAssembly, IDictionary<string, IEntityChange> changes)
        {
            var originalTypes = originalAssembly.MainModule.GetAllTypes().ToArray();
            var newTypes = currentAssembly.MainModule
                .GetAllTypes()
                .Where(IsNotCompilerGenerated)
                .Except(originalTypes.Where(t => t.IsPublicAPI()), TypeDefinitionEqualityComparer.Instance);

            foreach (var currentType in newTypes.Where(t => t.IsPublicAPI()))
            {
                if (HasTypeMoved(changes, currentType))
                    continue;

                var originalType = originalTypes.FirstOrDefault(TypeEqualityComparer.Instance.Equals);

                AddChange(changes, currentType, new TypeAddedChange(originalType, currentType));
            }
        }

        private bool HasTypeMoved(IDictionary<string, IEntityChange> changes, TypeDefinition type)
        {
            var typeMovedChecker = new TypeMovedChecker(type);
            foreach (var change in changes.Values)
            {
                change.Accept(typeMovedChecker);
                if (typeMovedChecker.CurrentTypeHasMoved)
                    return true;
            }

            return false;
        }

        private void ProcessType(TypeDefinition type, Dictionary<string, IEntityChange> changes)
        {
            if (type.Name == "<Module>")
                return;

            if (!type.IsPublicAPI())
                return;

            var resolved = ResolveTypeInCurrentVersion(type);
            if (resolved == null)
            {
                resolved = CheckMovedOrRemovedType(type, changes);
            }

            if (resolved != null)
            {
                CheckTypeInstancenessChanges(type, resolved, changes);
                CheckAccessibilityChanges(type, resolved, changes);
                CheckEntityTypeChanges(type, resolved, changes);
                CheckHierarchyChanges(type, resolved, changes);
                CheckObsoleteAttribute(type, resolved, changes);
                CheckAttributeChanges(type, resolved, changes);
                CheckSealdnessChanges(type, resolved, changes);

                ProcessMembers(type, resolved, changes);
            }

            foreach (var nestedType in type.NestedTypes)
            {
                ProcessType(nestedType, changes);
            }
        }

        private void CheckSealdnessChanges(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            if (currentType.IsValueType)
                return;

            if (HasTypeInstancenessChanged(currentType, originalType))
                return;

            if (!originalType.IsSealed && currentType.IsSealed)
                AddChange(changes, originalType, new SealednessChange(originalType, currentType));
        }

        private void CheckObsoleteAttribute(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            var change = originalType.CreateObsoleteAttributeChangeIfApplicable(currentType);
            if (change != null)
                AddChange(changes, originalType, change);
        }

        private void CheckAttributeChanges(TypeDefinition originalType, TypeDefinition currentType, IDictionary<string, IEntityChange> changes)
        {
            var change = ValidatorMixin.CreateAttributeChangeIfApplicable(originalType, currentType, PseudoAttributeFromTypeDefinition(originalType), PseudoAttributeFromTypeDefinition(currentType));
            if (change != null)
                AddChange(changes, currentType, change);
        }

        private void CheckHierarchyChanges(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            if (ContainsChangeOfType<EntityTypeChanged>(changes, originalType))
                return;

            if (originalType.BaseType.IsEqualsTo(currentType.BaseType))
                return;

            AddChange(changes, originalType, new TypeHierarchyChanged(originalType, currentType));
        }

        private void CheckTypeInstancenessChanges(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            if (HasTypeInstancenessChanged(originalType, currentType))
            {
                AddChange(changes, originalType, new InstancenessChange(originalType, currentType, InstancenessChangeKind.StaticToInstance));
            }
            else if (HasTypeInstancenessChanged(currentType, originalType))
            {
                AddChange(changes, originalType, new InstancenessChange(originalType, currentType, InstancenessChangeKind.InstanceToStatic));
            }
        }

        private void CheckEntityTypeChanges(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            if (originalType.Attributes == currentType.Attributes)
                return;

            if (originalType.IsEnum && currentType.IsEnum)
                return;

            if (originalType.IsInterface && currentType.IsInterface)
                return;

            if (originalType.IsValueType && currentType.IsValueType)
                return;

            if (originalType.IsClass && !originalType.IsEnum && !originalType.IsValueType
                && currentType.IsClass && !currentType.IsEnum && !currentType.IsValueType)
                return;

            AddChange(changes, originalType, new EntityTypeChanged(originalType, currentType));
        }

        private void CheckAccessibilityChanges(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            if (currentType.IsPublicAPI() || !originalType.IsPublicAPI() || DeclaringTypeHasChangedAccessibility(currentType.DeclaringType, originalType.DeclaringType))
                return;

            AddChange(changes, originalType, new TypeAccessibilityChange(originalType, currentType));
        }

        private bool DeclaringTypeHasChangedAccessibility(TypeDefinition current, TypeDefinition original)
        {
            if (current == null || original == null)
                return false;

            return current.IsPublicAPI() != original.IsPublicAPI();
        }

        private TypeDefinition CheckMovedOrRemovedType(TypeDefinition type, IDictionary<string, IEntityChange> changes)
        {
            var found = FindType(newAssembly, type);
            if (found == null)
            {
                AddChange(changes, type, new TypeRemoved(type));
            }
            else
                AddChange(changes, type, new TypeMoved(type, found));

            return found;
        }

        private void ProcessMembers(TypeDefinition originalType, TypeDefinition currentType, Dictionary<string, IEntityChange> changes)
        {
            new FieldChangesCollector(changes).ProcessMembers(originalType, currentType);
            new PropertyChangesCollector(changes).ProcessMembers(originalType, currentType);
            new EventChangesCollector(changes).ProcessMembers(originalType, currentType);
            new MethodChangesCollector(changes).ProcessMembers(originalType, currentType);
        }

        private void AddChange(IDictionary<string, IEntityChange> changes, TypeDefinition type, IAPIChange change)
        {
            IEntityChange typeChange;
            if (!changes.TryGetValue(type.FullName, out typeChange))
            {
                typeChange = new TypeChange(type);
                changes.Add(type.FullName, typeChange);
            }

            typeChange.Changes.Add(change);
        }

        private bool ContainsChangeOfType<T>(IDictionary<string, IEntityChange> changes, TypeDefinition type) where T : IAPIChange
        {
            if (!changes.TryGetValue(type.FullName, out var typeChanges))
                return false;

            return typeChanges.Changes.OfType<T>().Any();
        }

        private static bool HasTypeInstancenessChanged(TypeDefinition lhs, TypeDefinition rhs)
        {
            return lhs.IsSealed && lhs.IsAbstract && (!rhs.IsSealed || !rhs.IsAbstract);
        }

        private static TypeDefinition FindType(AssemblyDefinition lookIn, TypeDefinition tbf)
        {
            var candidates = lookIn.Modules.SelectMany(m => m.GetAllTypes()).Where(t => t.Name == tbf.Name && (t.IsEnum == tbf.IsEnum || t.IsValueType == tbf.IsValueType || t.IsClass == tbf.IsClass));
            if (candidates.Count() == 1)
                return candidates.First();

            //TODO: Handle multiple candidates in target
            return null;
        }

        private TypeDefinition ResolveTypeInCurrentVersion(TypeDefinition type)
        {
            if (type == null)
                return null;

            var tbr = new TypeReference(type.Namespace, type.Name, newAssembly.MainModule, newAssembly.MainModule)
            {
                DeclaringType = ResolveTypeInCurrentVersion(type.DeclaringType)
            };

            return tbr.Resolve();
        }

        private CustomAttribute[] PseudoAttributeFromTypeDefinition(TypeDefinition type)
        {
            return (type.Attributes & TypeAttributes.Serializable) == TypeAttributes.Serializable
                ? new[] { new CustomAttribute(type.Module.ImportReference(typeof(SerializableAttribute).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[0], null))) }
                : new CustomAttribute[0];
        }

        private static bool IsNotCompilerGenerated(IMemberDefinition entity)
        {
            if (!entity.HasCustomAttributes)
                return true;

            return !entity.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }
    }
}
