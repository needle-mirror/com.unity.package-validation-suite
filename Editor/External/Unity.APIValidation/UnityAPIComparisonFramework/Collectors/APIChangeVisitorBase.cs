using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    public class APIChangeVisitorBase : IAPIChangeVisitor
    {
        public virtual void Visit(TypeAddedChange change)
        {
        }

        public virtual void Visit(MemberAdded change)
        {
        }

        public virtual void Visit(TypeAccessibilityChange change)
        {
        }

        public virtual void Visit(MemberAccessibilityChange change)
        {
        }

        public virtual void Visit(ElementTypeChange change)
        {
        }

        public virtual void Visit(EntityTypeChanged change)
        {
        }

        public virtual void Visit(TypeHierarchyChanged change)
        {
        }

        public virtual void Visit(TypeMoved change)
        {
        }

        public virtual void Visit(TypeRemoved change)
        {
        }

        public virtual void Visit(MemberRemoved change)
        {
        }

        public virtual void Visit(InstancenessChange change)
        {
        }

        public virtual void Visit(FieldConstnessChange change)
        {
        }

        public virtual void Visit(MethodParameterCountChange change)
        {
        }

        public virtual void Visit(MethodParameterTypeChange change)
        {
        }

        public virtual void Visit(MethodVirtualnessChange change)
        {
        }

        public virtual void Visit(PropertyVirtualnessChange change)
        {
        }

        public virtual void Visit(ParameterReferencenessChange change)
        {
        }

        public virtual void Visit(ParameterDefaultnessChange change)
        {
        }

        public virtual void Visit(MethodAbstractnessChange change)
        {
        }

        public virtual void Visit(PropertyAbstractnessChange change)
        {
        }

        public virtual void Visit(ObsoleteAttributeChange change)
        {
        }

        public virtual void Visit(AttributeChange change)
        {
        }

        public virtual void Visit(SealednessChange change)
        {
        }

        public void Visit(ConstantValueChanged change)
        {
        }
    }
}
