using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public interface IEntityChange
    {
        void Accept(IEntityChangeVisitor visitor);
        List<IAPIChange> Changes { get; }
        IMemberDefinition Original { get; }
    }
}
