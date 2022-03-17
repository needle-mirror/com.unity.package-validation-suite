using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public abstract class EntityChangeBase : IEntityChange
    {
        protected EntityChangeBase(IMemberDefinition original)
        {
            Original = original;
        }

        public abstract void Accept(IEntityChangeVisitor visitor);
        public List<IAPIChange> Changes { get { return changes; } }
        public IMemberDefinition Original { get; protected set; }

        private List<IAPIChange> changes = new List<IAPIChange>();
    }
}
