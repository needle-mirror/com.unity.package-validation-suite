using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class InstancenessChange : APIChangeBase<IMemberDefinition>
    {
        public InstancenessChange(IMemberDefinition original, IMemberDefinition current, InstancenessChangeKind kind) : base(original, current)
        {
            Kind = kind;
        }

        public InstancenessChangeKind Kind { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
