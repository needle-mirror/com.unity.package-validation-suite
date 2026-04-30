using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MemberAccessibilityChange : APIChangeBase<IMemberDefinition>
    {
        public MemberAccessibilityChange(IMemberDefinition original, IMemberDefinition current) : base(original, current)
        {
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
