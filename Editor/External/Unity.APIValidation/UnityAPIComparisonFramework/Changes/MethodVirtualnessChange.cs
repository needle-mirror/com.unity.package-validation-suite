using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MethodVirtualnessChange : APIChangeBase<MethodDefinition>
    {
        public MethodVirtualnessChange(MethodDefinition original, MethodDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
