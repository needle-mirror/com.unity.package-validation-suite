using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class PropertyAbstractnessChange : APIChangeBase<PropertyDefinition>
    {
        public PropertyAbstractnessChange(PropertyDefinition original, PropertyDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
