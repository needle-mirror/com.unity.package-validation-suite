using System.Linq;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Descriptors
{
    internal class ChangeDescriptionFormatterVisitor : IAPIChangeVisitor
    {
        public void Visit(TypeAddedChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(MemberAdded change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(TypeAccessibilityChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(MemberAccessibilityChange change)
        {
            Description = string.Format(" {0} ({1} -> {2})", change.Original.FullName, change.Original.AccessibilityAsString(), change.Current.AccessibilityAsString());
        }

        public void Visit(ElementTypeChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(EntityTypeChanged change)
        {
            Description = change.Original.FullName + string.Format(" ({0} -> {1})", change.Original.Kind(), change.Current.Kind());
        }

        public void Visit(TypeHierarchyChanged change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(TypeMoved change)
        {
            Description = string.Format("{0} -> {1}", change.Original.FullName, change.Current.FullName);
        }

        public void Visit(TypeRemoved change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(MemberRemoved change)
        {
            CreateGenericDescription(change);
            Description = Description + string.Format(" ({0})", change.MemberKind);
        }

        public void Visit(InstancenessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(FieldConstnessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(MethodParameterCountChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(MethodParameterTypeChange change)
        {
            CreateGenericDescription(change);
            Description = Description + "\n\t\t" + string.Join("\n\t\t", change.Mismatches.Select(p => "(" + p.Name + ") " + p.ParameterType.Name + " -> " + change.CurrentEntity.Parameters[p.Index].ParameterType.Name).ToArray());
        }

        public void Visit(MethodVirtualnessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(PropertyVirtualnessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(ParameterReferencenessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(ParameterDefaultnessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(MethodAbstractnessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(PropertyAbstractnessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(ObsoleteAttributeChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(AttributeChange change)
        {
            CreateGenericDescription(change);
            if (change.Added.Any())
                Description = Description + string.Format("\n\t\t+ {0}", string.Join("\n\t", change.Added.Select(attr => attr.AttributeType.FullName).ToArray()));

            if (change.Removed.Any())
                Description = Description + string.Format("\n\t\t- {0}", string.Join("\n\t", change.Removed.Select(attr => attr.AttributeType.FullName).ToArray()));
        }

        public void Visit(SealednessChange change)
        {
            CreateGenericDescription(change);
        }

        public void Visit(ConstantValueChanged change)
        {
            CreateGenericDescription(change);
            Description = Description + string.Format("\n\t\t{0} value changed from {1} to {2}", change.ConstantKind == ConstantKind.EnumMember ? "enum member" : "constant field", change.OriginalEntity.Constant, change.CurrentEntity.Constant);
        }

        public string Description { get; private set; }

        private void CreateGenericDescription(IAPIChange change)
        {
            Description = (change.Original ?? change.Current).FullName;
        }
    }
}
