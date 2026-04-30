using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class ObsoleteAttributeChange : APIChangeBase<IMemberDefinition>
    {
        public ObsoleteAttributeChange(IMemberDefinition original, IMemberDefinition current, ObsoleteKind originalKind, ObsoleteKind currentKind, bool originalIsUpgradable, bool currentIsUpgradable) : base(original, current)
        {
            OriginalKind = originalKind;
            CurrentKind = currentKind;
            OriginalIsUpgradable = originalIsUpgradable;
            CurrentIsUpgradable = currentIsUpgradable;
        }

        public ObsoleteKind OriginalKind { get; private set; }
        public ObsoleteKind CurrentKind { get; private set; }
        public bool OriginalIsUpgradable { get; private set; }
        public bool CurrentIsUpgradable { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    public enum ObsoleteKind
    {
        None,
        Warning,
        Error
    }
}
