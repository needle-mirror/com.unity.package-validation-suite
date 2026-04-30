using Mono.Cecil;
using Unity.APIComparison.Framework.Collectors;

namespace Unity.APIComparison.Framework.Changes
{
    public class ConstantValueChanged : APIChangeBase<FieldDefinition>
    {
        public ConstantValueChanged(FieldDefinition original, FieldDefinition current, ConstantKind constantKind) : base(original, current)
        {
            ConstantKind = constantKind;
        }

        public ConstantKind ConstantKind { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
