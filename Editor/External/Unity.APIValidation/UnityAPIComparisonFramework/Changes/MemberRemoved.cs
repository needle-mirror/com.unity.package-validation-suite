using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MemberRemoved : APIChangeBase<IMemberDefinition>
    {
        public MemberRemoved(IMemberDefinition original, MemberKind kind) : base(original, null)
        {
            MemberKind = kind;
        }

        public MemberKind MemberKind { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
