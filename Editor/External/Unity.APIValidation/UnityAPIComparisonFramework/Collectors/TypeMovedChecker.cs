using System;
using Mono.Cecil;
using Unity.APIComparison.Framework.Changes;

namespace Unity.APIComparison.Framework.Collectors
{
    internal class TypeMovedChecker : APIChangeVisitorBase, IEntityChangeVisitor
    {
        private readonly TypeDefinition _type;

        public TypeMovedChecker(TypeDefinition type)
        {
            _type = type;
        }

        public bool CurrentTypeHasMoved { get; private set; }

        public void Visit(TypeChange typeChange)
        {
            foreach (var change in typeChange.Changes)
            {
                change.Accept(this);
            }
        }

        public override void Visit(TypeMoved change)
        {
            if (TypeReferenceComparer.Instance.Equals((TypeReference) change.Current, _type))
                CurrentTypeHasMoved = true;
        }

        public override void Visit(TypeRemoved change)
        {
        }
    }
}
