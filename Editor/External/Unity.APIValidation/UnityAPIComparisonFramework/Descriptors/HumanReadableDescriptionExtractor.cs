using System.Linq;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Descriptors
{
    public class HumanReadableDescriptionExtractor : IAPIChangeVisitor
    {
        private string m_Description;
        public string Description { get { return m_Description; } }


        public void Visit(TypeAddedChange change)
        {
            m_Description = string.Format("Type '{0}' added.", change.Current.FullName);
        }

        public void Visit(MemberAdded change)
        {
            m_Description = string.Format("Member '{0}' added.", change.Current.FullName);
        }

        public void Visit(TypeAccessibilityChange change)
        {
            m_Description = string.Format("Accessibility of type '{0}' changed.", change.CurrentEntity.Name);
        }

        public void Visit(MemberAccessibilityChange change)
        {
            m_Description = string.Format("Accessibility of member '{0}' changed.", change.CurrentEntity.Name);
        }

        public void Visit(ElementTypeChange change)
        {
            //TODO: Add the original / current element types
            m_Description = string.Format("Element type of member '{0}' changed.", change.CurrentEntity.Name);
        }

        public void Visit(EntityTypeChanged change)
        {
            m_Description = string.Format("'{0}' changed from '{1}' to '{2}'.", change.CurrentEntity.Name, change.OriginalEntity.TypeKind(), change.CurrentEntity.TypeKind());
        }

        public void Visit(TypeHierarchyChanged change)
        {
            m_Description = string.Format("Base type of class '{0}' changed from '{1}' to '{2}'.", change.CurrentEntity.Name, change.OriginalEntity.BaseType, change.CurrentEntity.BaseType);
        }

        public void Visit(TypeMoved change)
        {
            m_Description = string.Format("Type '{0}' moved from '{1}' to '{2}'.", change.CurrentEntity.Name, change.OriginalEntity.Namespace, change.CurrentEntity.Namespace);
        }

        public void Visit(TypeRemoved change)
        {
            m_Description = string.Format("Type '{0}' has been deleted.", change.OriginalEntity.FullName);
        }

        public void Visit(MemberRemoved change)
        {
            m_Description = string.Format("Member '{0}.{1}' has been deleted ({2}).", change.OriginalEntity.DeclaringType.Name, change.OriginalEntity.Name, change.OriginalEntity.FullName);
        }

        public void Visit(InstancenessChange change)
        {
            m_Description = string.Format("Instanceness of '{0}' has has changed: {1}.", change.Original.FullName, change.Kind == InstancenessChangeKind.InstanceToStatic ? "instance -> static" : "static -> instance");
        }

        public void Visit(FieldConstnessChange change)
        {
            m_Description = string.Format("Field '{0}' has changed from '{1}' to '{2}'.", change.Original.FullName, change.OriginalEntity.IsInitOnly ? "readonly" : "const", change.CurrentEntity.IsInitOnly ? "readonly" : "const");
        }

        public void Visit(MethodParameterCountChange change)
        {
            m_Description = string.Format("# of parameters of method '{0}' has changed from '{1}' -> '{2}'.", change.OriginalEntity.Name, change.OriginalEntity.Parameters.Count, change.CurrentEntity.Parameters.Count);
        }

        public void Visit(MethodParameterTypeChange change)
        {
            m_Description = string.Format("One or more parameters type of method '{0}' changed: '{1}'.", change.OriginalEntity.FullName, change.Mismatches.Aggregate("", (acc, curr) => curr.Name + "(" + curr.Index + ") : " + curr.ParameterType.Name + ", " + acc));
        }

        public void Visit(MethodVirtualnessChange change)
        {
            m_Description = string.Format("Method '{0}' virtual modifier changed {1}.", change.OriginalEntity.FullName, change.OriginalEntity.IsVirtual ? "virtual -> non virtual" : "non virtual -> virtual");
        }

        public void Visit(PropertyVirtualnessChange change)
        {
            m_Description = string.Format("Getter/Setter accessor of property '{0}' virtual modifier changed.", change.OriginalEntity.FullName);
        }

        public void Visit(ParameterReferencenessChange change)
        {
            var details = change.Mismatches.Aggregate("", (acc, curr) => string.Format("{0}:{1} ({2}) {3} -> {4}", curr.Original.Name, curr.Original.ParameterType.Name, curr.Original.Index, curr.Original.Kind(), curr.Current.Kind()) + ", " + acc);
            m_Description = string.Format("One or more parameters of method '{0}' changed its ref/out modifiers: '{1}'.", change.OriginalEntity.FullName, details);
        }

        public void Visit(ParameterDefaultnessChange change)
        {
            var details = change.Mismatches.Aggregate("", (acc, cur) => cur.Name + "(" + cur.Index + ") : " + (cur.HasDefault ? cur.Constant : " - ") + ", " + acc);
            m_Description = string.Format("One or more parameters of method '{0}' changed its default value: '{1}'.", change.OriginalEntity.FullName, details);
        }

        public void Visit(MethodAbstractnessChange change)
        {
            m_Description = string.Format("Abstract modifier of method '{0}' changed: '{1}'.", change.OriginalEntity.FullName, change.OriginalEntity.IsAbstract ? "abstract -> non abstract" : "non abstract -> abstract");
        }

        public void Visit(PropertyAbstractnessChange change)
        {
            m_Description = string.Format("Abstract modifier of property '{0}' changed: '{1}'.", change.OriginalEntity.FullName, (change.OriginalEntity.GetMethod ?? change.OriginalEntity.SetMethod).IsAbstract ? "abstract -> non abstract" : "non abstract -> abstract");
        }

        public void Visit(ObsoleteAttributeChange change)
        {
            m_Description = string.Format("ObsolteAttribute on '{0}' changed '{1} -> {2}'.", change.OriginalEntity.FullName, change.OriginalKind, change.CurrentKind);
        }

        public void Visit(AttributeChange change)
        {
            m_Description = string.Format("Attributes on entity '{0}' changed: +[{1}] -[{2}].", change.OriginalEntity.FullName, change.Added.Aggregate("", (acc, curr) => curr.AttributeType.Name + ", " + acc), change.Removed.Aggregate("", (acc, curr) => curr.AttributeType.Name + ", " + acc));
        }

        public void Visit(SealednessChange change)
        {
            m_Description = string.Format("Class '{0}' changed from {1} to {2}.", change.OriginalEntity.FullName, change.OriginalEntity.IsSealed ? "sealed" : "non sealed", change.CurrentEntity.IsSealed ? "sealed" : "non sealed");
        }

        public void Visit(ConstantValueChanged change)
        {
            m_Description = string.Format("{0} '{1}' changed from {2} to {3}", change.ConstantKind == ConstantKind.EnumMember ? "enum member" : "Constant value", change.CurrentEntity.Name, change.OriginalEntity.Constant, change.CurrentEntity.Constant);
        }
    }
}
