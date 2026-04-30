using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class TypeHierarchyChanged : APIChangeBase<TypeDefinition>
    {
        public TypeHierarchyChanged(TypeDefinition original, TypeDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
