using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.APIComparison.Framework.Changes
{
    public class ParameterDefaultnessChange : APIChangeBase<MethodDefinition>
    {
        public ParameterDefaultnessChange(MethodDefinition original, MethodDefinition current, IEnumerable<ParameterDefinition> mismatches) : base(original, current)
        {
            Mismatches = mismatches;
        }

        public IEnumerable<ParameterDefinition> Mismatches { get; private set; }

        public override void Accept(IAPIChangeVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
