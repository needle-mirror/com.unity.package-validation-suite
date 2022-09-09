using System;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.APIComparison.Framework.Changes;
using Unity.APIComparison.Framework.Collectors;

namespace Unity.APIComparison.Framework
{
    public static class CecilExtensions
    {
        public static MethodDefinition FindOverridenMethod(this MethodDefinition tbc)
        {
            if (!tbc.IsVirtual || !tbc.IsReuseSlot)
            {
                // it is not an override (see https://stackoverflow.com/a/8103611/157321)
                return null;
            }

            var typeToCheck = tbc.DeclaringType.BaseType.Resolve();
            while (typeToCheck != null)
            {
                var found = typeToCheck.Methods.FirstOrDefault(c => MethodDefinitionSignatureComparer.Instance.Equals(c, tbc));
                if (found != null) 
                    return found;

                if (typeToCheck.BaseType == null)
                    throw new InvalidOperationException($"Could not find suitable overriding method for '{tbc.FullName}'");
                
                typeToCheck = typeToCheck.BaseType.Resolve();
            }

            return null;
        }

        public static bool IsType(this IMemberDefinition toBeChecked)
        {
            return toBeChecked is TypeDefinition;
        }

        public static bool IsAbstract(this PropertyDefinition property)
        {
            var m = property.GetMethod ?? property.SetMethod;
            if (m == null)
                return false;

            return m.IsAbstract;
        }

        public static string AccessibilityAsString(this IMemberDefinition member)
        {
            var field = member as FieldDefinition;
            if (field != null)
                return Enum.GetName(typeof(FieldAttributes), field.Attributes & FieldAttributes.FieldAccessMask);

            var method = member as MethodDefinition;
            if (method != null)
                return Enum.GetName(typeof(MethodAttributes), method.Attributes & MethodAttributes.MemberAccessMask);

            var property = member as PropertyDefinition;
            if (property != null && property.GetMethod != null)
                return Enum.GetName(typeof(MethodAttributes), property.GetMethod.Attributes & MethodAttributes.MemberAccessMask);

            if (property != null && property.SetMethod != null)
                return Enum.GetName(typeof(MethodAttributes), property.SetMethod.Attributes & MethodAttributes.MemberAccessMask);

            return "(cannot get string version of accessibility for " + member.FullName + " of type " + member.GetType().Name + ")";
        }

        public static TypeReference ElementType(this IMemberDefinition member)
        {
            var field = member as FieldDefinition;
            if (field != null)
                return field.FieldType;

            var method = member as MethodDefinition;
            if (method != null)
                return method.ReturnType;

            var property = member as PropertyDefinition;
            if (property != null)
                return property.PropertyType;

            var evt = member as EventDefinition;
            if (evt != null)
                return evt.EventType;

            throw new Exception("cannot get element type for " + member.FullName + " of type " + member.GetType().Name);
        }

        public static bool IsPublicAPI(this EventDefinition eventDefinition)
        {
            return IsPublicMethodTupleBasedEntityAPI(eventDefinition.AddMethod, eventDefinition.RemoveMethod);
        }

        public static bool IsPublicAPI(this TypeDefinition type)
        {
            //TODO: This is a workaround for types having its namespace changed
            //      correct solution is to somehow take ApiUpdater configs into
            //      account. Most likely we want to introduce some interfaces 
            //      that given a type / member reference decides whether it 
            //      needs to be processed or not and accept assemblies implementing
            //      that interfaces so we could create one implementation that
            //      would check in ApiUpdater configs.
            if (type == null)
            {
                return false;
            }
                
            if (type.DeclaringType != null && !type.DeclaringType.IsPublicAPI())
                return false;

            return type.IsPublic
                || type.IsNestedPublic
                || type.IsNestedFamily
                || type.IsNestedFamilyOrAssembly;
        }

        public static bool IsPublicAPI(this FieldDefinition field)
        {
            //TODO: This is a workaround for types having its namespace changed
            //      correct solution is to somehow take ApiUpdater configs into
            //      account. Most likely we want to introduce some interfaces 
            //      that given a type / member reference decides whether it 
            //      needs to be processed or not and accept assemblies implementing
            //      that interfaces so we could create one implementation that
            //      would check in ApiUpdater configs.
            if (field == null) return false;
            
            return field.IsPublic || field.IsFamily;
        }

        public static bool IsPublicAPI(this PropertyDefinition property)
        {
            return IsPublicMethodTupleBasedEntityAPI(property.GetMethod, property.SetMethod);
        }

        public static bool IsPublicAPI(this MethodDefinition method)
        {
            //TODO: This is a workaround for types having its namespace changed
            //      correct solution is to somehow take ApiUpdater configs into
            //      account. Most likely we want to introduce some interfaces 
            //      that given a type / member reference decides whether it 
            //      needs to be processed or not and accept assemblies implementing
            //      that interfaces so we could create one implementation that
            //      would check in ApiUpdater configs.
            if (method == null) return false;
            return method.IsPublic || method.IsFamily;
        }

        public static bool IsPublicMethodTupleBasedEntityAPI(MethodDefinition m1, MethodDefinition m2)
        {
            var isM1Public = m1 != null && m1.IsPublicAPI();
            var isM2Public = m2 != null && m2.IsPublicAPI();

            return isM1Public || isM2Public;
        }

        public static bool IsEnumBackingField(this FieldDefinition field)
        {
            return field.DeclaringType.IsEnum && field.IsRuntimeSpecialName && field.Name == "value__";
        }

        public static bool IsEnumBackingField(this IMemberDefinition member)
        {
            var fieldDefinition = member as FieldDefinition;
            return fieldDefinition != null ? fieldDefinition.IsEnumBackingField() : false;
        }

        public static bool IsEqualsTo(this TypeReference self, TypeReference other)
        {
            if (self != null ^ other != null) // if they are different, return false
                return false;

            if (self == null)
                return true;

            return TypeEqualityComparer.Instance.Equals(self, other);
        }

        public static T MapCustomAttributeCtorParameter<S, T>(this CustomAttribute self, int index, Func<S, T> mapper)
        {
            if (!self.HasConstructorArguments)
                return default(T);

            if (self.ConstructorArguments.Count <= index)
                return default(T);

            return mapper((S)self.ConstructorArguments[index].Value);
        }

        public static bool IsKind(this IMemberDefinition member, MemberKind kind)
        {
            switch (kind)
            {
                case MemberKind.Property: return member is PropertyDefinition;
                case MemberKind.Field: return member is FieldDefinition;
                case MemberKind.Event: return member is EventDefinition;
                case MemberKind.Method: 
                case MemberKind.PropertyGetter:
                case MemberKind.PropertySetter:
                    if (member is MethodDefinition m)
                    {
                        if (m.IsGetter)
                            return kind == MemberKind.PropertyGetter;
                
                        if (m.IsSetter)
                            return kind == MemberKind.PropertySetter;
                
                        return kind == MemberKind.Method;
                    }
                    return false;
                
                case MemberKind.Delegate:
                    var typeDef = member as TypeDefinition;
                    if (typeDef == null)
                        throw new Exception("Member definition type not supported: " + member.GetType().FullName);

                    return typeDef.BaseType != null && typeDef.BaseType.FullName == typeof(MulticastDelegate).FullName;
            }

            return false;
        }
        
        public static string Kind(this IMemberDefinition member)
        {
            if (member is MethodDefinition)
                return "method";

            if (member is PropertyDefinition)
                return "property";

            if (member is EventDefinition)
                return "event";

            if (member is FieldDefinition)
                return "field";

            var typeDef = member as TypeDefinition;
            if (typeDef == null)
                throw new Exception("Member definition type not supported: " + member.GetType().FullName);

            if (typeDef.BaseType != null && typeDef.BaseType.FullName == typeof(MulticastDelegate).FullName)
                return "delegate";

            return TypeKind(typeDef);
        }

        public static string TypeKind(this TypeDefinition type)
        {
            if (type.IsEnum) return "enum";
            if (type.IsValueType) return "struct";
            if (type.IsClass) return "class";
            if (type.IsInterface) return "interface";

            return string.Format("Type's '{0}' type ({1}) is not supported.", type.FullName, type.GetType().FullName);
        }

        public static ParameterKind Kind(this ParameterDefinition parameter)
        {
            if (!parameter.ParameterType.IsByReference)
                return ParameterKind.ByValue;

            return parameter.IsOut ? ParameterKind.Out : ParameterKind.ByRef;
        }


        public static bool IsOverride(this IMemberDefinition member)
        {
            if (member is MethodDefinition method)
                return method.IsVirtual && !method.IsNewSlot && !method.IsFinal;
            if (member is PropertyDefinition property)
                return property.GetMethod.IsOverride() || property.SetMethod.IsOverride();
            if (member is EventDefinition @event)
                return @event.AddMethod.IsOverride();

            return false;
        }
        
        public static bool IsVirtual(this MethodDefinition method)
        {
            return method.IsVirtual && !method.IsFinal && !method.IsAbstract;
        }
        
        public static ModifierChangeKind VirtualnessChanged(this MethodDefinition self, MethodDefinition other)
        {
            if (self == null)
                return ModifierChangeKind.NoChange;

            if (other == null)
                return ModifierChangeKind.NoChange;

            var virtualChanged = self.IsVirtual() ^ other.IsVirtual();
            if (virtualChanged)
                return self.IsVirtual ? ModifierChangeKind.Removed : ModifierChangeKind.Added;
            
            return ModifierChangeKind.NoChange;
        }
        
        public static ModifierChangeKind AbstractnessChanged(this MethodDefinition self, MethodDefinition other)
        {
            if (self == null)
                return ModifierChangeKind.NoChange;

            if (other == null)
                return ModifierChangeKind.NoChange;

            if (self.IsAbstract ^ other.IsAbstract)
                return self.IsAbstract ? ModifierChangeKind.Removed : ModifierChangeKind.Added;

            return ModifierChangeKind.NoChange;
        }

        public static bool IsEventMethod(this MethodDefinition method)
        {
            return method.ReturnType == method.DeclaringType.Module.TypeSystem.Void
                && method.Parameters.Count == 1
                && (method.Name.StartsWith("add_") || method.Name.StartsWith("remove_"));
        }

        public static bool HasAttribute<T>(this IMemberDefinition member) where T : Attribute
        {
            if (member == null || !member.HasCustomAttributes)
                return false;

            var fullName = typeof(T).FullName;
            foreach (var x in member.CustomAttributes)
            {
                if (string.CompareOrdinal(x.AttributeType.FullName, fullName) == 0)
                    return true;
            }

            return false;
        }

        /**
         *  This method checks if 'method' is a parameterless ctor and if its body looks like the following code:
         *
         *      IL_0000: ldarg.0
         *      IL_0001: call instance void [mscorlib]System.Object::.ctor()
         *      IL_0006: ret
         *
         */
        public  static bool LooksLikeDefaultCtor(this MethodDefinition method, TypeDefinition typeMissingMember = null)
        {
            if (!method.IsConstructor || method.Parameters.Count != 0)
                return false;

            if (method.Body == null) // icall
                return false;

            if (method.Body.Instructions.Count != 3)
                return false;

            var baseCtorCall = method.Body.Instructions[1];
            if (baseCtorCall.OpCode != OpCodes.Call)
                return false;

            var calledMethodRef = baseCtorCall.Operand as MethodReference;
            var calledMethod = calledMethodRef != null ? calledMethodRef.Resolve() : null;

            if (calledMethod == null)
                return false;

            return calledMethod.IsConstructor && calledMethod.Parameters.Count == 0;
        }

        public static bool IsEnum(this IMemberDefinition candidate)
        {
            var typeDefinition = candidate as TypeDefinition;
            if (typeDefinition == null)
                return false;

            return typeDefinition.IsEnum;
        }

        public static string GetSourcePathFromDebugInformation(this IMemberDefinition member)
        {
            var sequencePoint = GetSequencePointFor(member as MethodDefinition)
                ?? GetSequencePointFor(member as FieldDefinition)
                ?? GetSequencePointFor(member as PropertyDefinition)
                ?? GetSequencePointFor(member as EventDefinition)
                ?? GetSequencePointFor(member as TypeDefinition);

            if (sequencePoint == null)
                return "N/A";

            return $"{sequencePoint.Document.Url} ({sequencePoint.StartLine}, {sequencePoint.StartColumn})";
        }

        private static SequencePoint GetSequencePointFor(FieldDefinition field)
        {
            if (field == null)
                return null;

            return GetSequencePointFor(field.DeclaringType);
        }

        private static SequencePoint GetSequencePointFor(TypeDefinition type)
        {
            if (type == null)
                return null;

            var methods = type.Methods
                .Where(m => m.HasBody);
            foreach (var method in methods)
            {
                var sequencePoint = method.Body.Instructions.Select(inst => GetSequencePointFor(inst, method))
                    .FirstOrDefault(seq => seq?.Document != null);

                if (sequencePoint != null)
                    return sequencePoint;
            }

            return null;
        }

        private static SequencePoint GetSequencePointFor(EventDefinition evt)
        {
            if (evt == null)
                return null;

            return GetSequencePointFor(evt.AddMethod)
                ?? GetSequencePointFor(evt.RemoveMethod);
        }

        private static SequencePoint GetSequencePointFor(PropertyDefinition property)
        {
            if (property == null)
                return null;

            return GetSequencePointFor(property.SetMethod)
                ?? GetSequencePointFor(property.GetMethod);
        }

        private static SequencePoint GetSequencePointFor(MethodDefinition method)
        {
            if (method == null || !method.HasBody)
                return null;

            return method.Body.Instructions
                .Select(inst => GetSequencePointFor(inst, method))
                .FirstOrDefault(sp => sp != null);
        }

        private static SequencePoint GetSequencePointFor(Instruction instruction, MethodDefinition methodDefinition)
        {
            return methodDefinition.DebugInformation.GetSequencePoint(instruction);
        }

        public static bool IsEnumMember(this FieldDefinition candidate)
        {
            return candidate.DeclaringType.IsEnum && candidate.FieldType == candidate.DeclaringType;
        }
    }
}
