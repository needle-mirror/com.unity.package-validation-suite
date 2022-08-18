using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class TypeAddedChange : APIChangeBase<TypeDefinition>
    {
        public override bool IsAdd() => true;

        public TypeAddedChange(TypeDefinition original, TypeDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
