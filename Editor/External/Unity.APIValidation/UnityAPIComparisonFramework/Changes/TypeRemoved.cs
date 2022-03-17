using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class TypeRemoved : APIChangeBase<TypeDefinition>
    {
        public TypeRemoved(TypeDefinition original) : base(original, null)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override string ToString()
        {
            return "Type removed: " + Original.FullName;
        }
    }
}
