using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class PropertyVirtualnessChange : APIChangeBase<PropertyDefinition>
    {
        public PropertyVirtualnessChange(PropertyDefinition original, PropertyDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
