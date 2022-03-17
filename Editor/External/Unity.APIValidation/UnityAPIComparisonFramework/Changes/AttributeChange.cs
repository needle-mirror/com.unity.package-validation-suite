using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class AttributeChange : APIChangeBase<IMemberDefinition>
    {
        public AttributeChange(IMemberDefinition original, IMemberDefinition current, IEnumerable<CustomAttribute> added, IEnumerable<CustomAttribute> removed) : base(original, current)
        {
            Added = added;
            Removed = removed;
        }

        public IEnumerable<CustomAttribute> Added { get; private set; }

        public IEnumerable<CustomAttribute> Removed { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
