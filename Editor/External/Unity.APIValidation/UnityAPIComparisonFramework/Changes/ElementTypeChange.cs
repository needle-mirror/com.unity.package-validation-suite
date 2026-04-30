using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class ElementTypeChange : APIChangeBase<IMemberDefinition>
    {
        public ElementTypeChange(IMemberDefinition original, IMemberDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
