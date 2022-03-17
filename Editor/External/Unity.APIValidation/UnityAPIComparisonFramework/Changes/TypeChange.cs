using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class TypeChange : EntityChangeBase
    {
        internal TypeChange(TypeDefinition original) : this(original, null)
        {
        }

        internal TypeChange(TypeDefinition original, TypeDefinition current) : base(original)
        {
            CurrentType = current;
        }

        public override void Accept(IEntityChangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public TypeDefinition OriginalType { get { return (TypeDefinition)Original; } }
        public TypeDefinition CurrentType { get; private set; }
    }
}
