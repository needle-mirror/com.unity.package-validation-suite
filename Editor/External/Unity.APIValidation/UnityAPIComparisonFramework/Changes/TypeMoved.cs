using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class TypeMoved : APIChangeBase<TypeDefinition>
    {
        public TypeMoved(TypeDefinition original, TypeDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
