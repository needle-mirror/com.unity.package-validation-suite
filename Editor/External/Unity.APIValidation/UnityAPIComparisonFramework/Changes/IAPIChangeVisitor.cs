namespace Unity.APIComparison.Framework.Changes
{
    public interface IAPIChangeVisitor
    {
        void Visit(TypeAddedChange change);
        void Visit(MemberAdded change);

        void Visit(TypeAccessibilityChange change);
        void Visit(MemberAccessibilityChange change);
        void Visit(ElementTypeChange change);
        void Visit(EntityTypeChanged change);
        void Visit(TypeHierarchyChanged change);
        void Visit(TypeMoved change);
        void Visit(TypeRemoved change);
        void Visit(MemberRemoved change);
        void Visit(InstancenessChange change);
        void Visit(FieldConstnessChange change);
        void Visit(MethodParameterCountChange change);
        void Visit(MethodParameterTypeChange change);
        void Visit(MethodVirtualnessChange change);
        void Visit(PropertyVirtualnessChange change);
        void Visit(ParameterReferencenessChange change);
        void Visit(ParameterDefaultnessChange change);
        void Visit(MethodAbstractnessChange change);
        void Visit(PropertyAbstractnessChange change);
        void Visit(ObsoleteAttributeChange change);
        void Visit(AttributeChange change);
        void Visit(SealednessChange change);
        void Visit(ConstantValueChanged change);
    }
}
