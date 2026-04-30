using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class TypeAccessibilityChange : APIChangeBase<TypeDefinition>
    {
        public TypeAccessibilityChange(TypeDefinition original, TypeDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
