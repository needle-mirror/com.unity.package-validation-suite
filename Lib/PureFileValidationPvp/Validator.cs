using System;
using System.Collections.Generic;
using System.Linq;

namespace PureFileValidationPvp
{
    public class Validator
    {
        /// All PVP checks implemented by Validator.
        public static readonly IReadOnlyList<string> Checks =
            ManifestValidations.Checks
            .Concat(PathValidations.Checks)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        public void Validate(IPackage package, Action<string, string> addError)
        {
            ManifestValidations.Run(package, addError);
            PathValidations.Run(package, addError);
        }
    }
}
