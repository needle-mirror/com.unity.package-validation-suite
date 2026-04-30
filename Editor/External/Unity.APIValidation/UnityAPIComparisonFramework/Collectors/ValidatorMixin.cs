using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    internal static class ValidatorMixin
    {
        private static IEqualityComparer<CustomAttribute> s_CustomAttributeComparer = new CustomAttributeTypeNameEqualityComparer();

        private static HashSet<string> s_AttributesWhiteList = new HashSet<string>
        {
            "UnityEngine.WrapperlessIcall",
            "System.ObsoleteAttribute",
            "UnityEngine.Scripting.GeneratedByOldBindingsGeneratorAttribute",
            typeof(MethodImplAttribute).FullName
        };
        private static ConcurrentDictionary<string, bool> s_IsPublicApiCache = new ConcurrentDictionary<string, bool>();

        public static ObsoleteAttributeChange CreateObsoleteAttributeChangeIfApplicable(this IMemberDefinition previous, IMemberDefinition current)
        {
            var previousObsoleteAttr = ObsoleteAttributeFrom(previous);
            var currentObsoleteAttr = ObsoleteAttributeFrom(current);

            var previousObsoleteKind = previousObsoleteAttr == null ? ObsoleteKind.None : previousObsoleteAttr.MapCustomAttributeCtorParameter<bool, ObsoleteKind>(1, b => b ? ObsoleteKind.Error : ObsoleteKind.Warning);
            var currentObsoleteKind = currentObsoleteAttr == null ? ObsoleteKind.None : currentObsoleteAttr.MapCustomAttributeCtorParameter<bool, ObsoleteKind>(1, b => b ? ObsoleteKind.Error : ObsoleteKind.Warning);

            if (IsNewObsoleteAttributeUsageMoreRestrictive(previousObsoleteKind, currentObsoleteKind, previousObsoleteAttr, currentObsoleteAttr) ||
                (!previousObsoleteAttr.IsUnityUpgradable() && currentObsoleteAttr.IsUnityUpgradable()))
                return new ObsoleteAttributeChange(previous, current, previousObsoleteKind, currentObsoleteKind, previousObsoleteAttr.IsUnityUpgradable(), currentObsoleteAttr.IsUnityUpgradable());

            return null;
        }

        private static CustomAttribute ObsoleteAttributeFrom(ICustomAttributeProvider member)
        {
            if (!member.HasCustomAttributes)
                return null;

            var obsoleteAttr = member.CustomAttributes.SingleOrDefault(attr => attr.AttributeType.FullName == typeof(ObsoleteAttribute).FullName);
            return obsoleteAttr;
        }

        private static bool IsUnityUpgradable(this CustomAttribute attr)
        {
            if (attr == null)
                return false;

            if (!attr.HasConstructorArguments)
                return false;

            return ((string)attr.ConstructorArguments[0].Value).IndexOf("(UnityUpgradable)") >= 0;
        }

        private static bool IsNewObsoleteAttributeUsageMoreRestrictive(ObsoleteKind previousObsoleteKind, ObsoleteKind currentObsoleteKind, CustomAttribute previousObsoleteAttr, CustomAttribute currentObsoleteAttr)
        {
            var c1 = currentObsoleteKind == ObsoleteKind.Error && previousObsoleteKind != ObsoleteKind.Error;
            var c2 = currentObsoleteKind == ObsoleteKind.Error 
                && previousObsoleteKind == ObsoleteKind.Error 
                && (currentObsoleteAttr.IsUnityUpgradable() ^ previousObsoleteAttr.IsUnityUpgradable());

            return c1 || c2;
        }

        public static IAPIChange CreateAttributeChangeIfApplicable(IMemberDefinition original, IMemberDefinition current, CustomAttribute[] originalPseudoAttributes, CustomAttribute[] currentPseudoAttributes)
        {
            if (!original.HasCustomAttributes && originalPseudoAttributes.Length == 0 &&
                !current.HasCustomAttributes && currentPseudoAttributes.Length == 0)
                return null;

            var originalAttributes = OrderedAttributeListFor(original, WhitelistFilter).Concat(originalPseudoAttributes).ToArray();
            var currentAttributes = OrderedAttributeListFor(current, WhitelistFilter).Concat(currentPseudoAttributes).ToArray();

            var addedAttrs = currentAttributes.Where(a => originalAttributes.All(oa => !s_CustomAttributeComparer.Equals(a, oa))).ToArray();
            var removedAttrs = originalAttributes.Where(a => currentAttributes.All(oa => !s_CustomAttributeComparer.Equals(a, oa))).ToArray();

            if (!addedAttrs.Any() && !removedAttrs.Any())
                return null;

            return new AttributeChange(original, current, addedAttrs, removedAttrs);
        }

        private static bool WhitelistFilter(CustomAttribute attr)
        {
            return IsPublicApiCached(attr) && !s_AttributesWhiteList.Contains(attr.AttributeType.FullName);
        }

        private static bool IsPublicApiCached(CustomAttribute attr)
        {
            var attributeTypeFullName = attr.AttributeType.FullName;
            try
            {
                return s_IsPublicApiCache.GetOrAdd(attributeTypeFullName, (st) => attr.AttributeType.Resolve().IsPublicAPI());
            }
            catch (AssemblyResolutionException)
            {
                // it is very unlikely that the attribute is question is defined in the assembly under test
                // in this case simply do not validate it.
                return false;
            }
        }

        private static IEnumerable<CustomAttribute> OrderedAttributeListFor(IMemberDefinition member, Func<CustomAttribute, bool> whitelistFilter)
        {
            if (!member.HasCustomAttributes)
                return Array.Empty<CustomAttribute>();

            return member.CustomAttributes.Where(whitelistFilter);
        }
    }
}
