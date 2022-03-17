using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class MemberAdded : APIChangeBase<IMemberDefinition>
    {
        public override bool IsAdd() => true;

        public IMemberDefinition Overrides { get; }

        public MemberAdded(IMemberDefinition current, string dataToBeHashed) : this(current, null, dataToBeHashed)
        {
        }

        public MemberAdded(IMemberDefinition current, IMemberDefinition overrides, string dataToBeHashed) : base(null, current, dataToBeHashed)
        {
            Overrides = overrides;
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
