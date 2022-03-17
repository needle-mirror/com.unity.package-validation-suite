using System.Collections;
using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public interface IAPIChange
    {
        void Accept(IAPIChangeVisitor visitor);

        IMemberDefinition Original { get; }

        IMemberDefinition Current { get; }

        string Hash { get; }

        string SourcePath { get; }

        IList<string> AffectedPlatforms { get; }

        bool IsAdd();
    }
}
