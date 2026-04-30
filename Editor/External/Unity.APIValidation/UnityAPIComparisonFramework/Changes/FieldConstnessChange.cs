using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class FieldConstnessChange : APIChangeBase<FieldDefinition>
    {
        public FieldConstnessChange(FieldDefinition original, FieldDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
