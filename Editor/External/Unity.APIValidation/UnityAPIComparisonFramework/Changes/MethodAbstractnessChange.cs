using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MethodAbstractnessChange : APIChangeBase<MethodDefinition>
    {
        public MethodAbstractnessChange(MethodDefinition original, MethodDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
