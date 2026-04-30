using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MethodParameterCountChange : APIChangeBase<MethodDefinition>
    {
        public MethodParameterCountChange(MethodDefinition original, MethodDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
