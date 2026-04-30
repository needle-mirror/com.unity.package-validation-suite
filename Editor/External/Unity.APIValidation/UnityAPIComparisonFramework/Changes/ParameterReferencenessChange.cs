using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public struct ParameterMismatch
    {
        public ParameterDefinition Original { get; set; }
        public ParameterDefinition Current { get; set; }

        public ParameterMismatch(ParameterDefinition original, ParameterDefinition current)
        {
            Original = original;
            Current = current;
        }
    }
    public class ParameterReferencenessChange : APIChangeBase<MethodDefinition>
    {
        public ParameterReferencenessChange(MethodDefinition original, MethodDefinition current, IEnumerable<ParameterMismatch> mismatches) : base(original, current)
        {
            Mismatches = mismatches;
        }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }

        public IEnumerable<ParameterMismatch> Mismatches { get; }
    }

    public enum ParameterKind
    {
        ByValue,
        ByRef,
        Out,
    }
}
