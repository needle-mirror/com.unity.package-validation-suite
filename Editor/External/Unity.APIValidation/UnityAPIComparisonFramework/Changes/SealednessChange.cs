using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class SealednessChange : APIChangeBase<TypeDefinition>
    {
        public SealednessChange(TypeDefinition originalType, TypeDefinition currentType) : base(originalType, currentType)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
