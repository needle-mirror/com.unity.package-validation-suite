using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace UnityEditor.PackageManager.ValidationSuite
{
    /// <summary>
    /// Class that manages the validation exceptions for this package.
    /// </summary>
    public class ValidationExceptionManager
    {
        private const string ValidationExceptionsFileName = "ValidationExceptions.json";

        private Dictionary<string, ValidationException> exceptionDictionary;

        /// <summary>
        /// Will return true if the package contains 1 or more validation exceptions
        /// </summary>
        public bool HasExceptions
        {
            get { return exceptionDictionary.Any(); }
        }

        /// <summary>
        /// Will return true if the package contains 1 or more validation exceptions
        /// </summary>
        public bool IsValid
        {
            get
            {
                return ExceptionsList.All(vt => vt.Used);
            }
        }

        internal IEnumerable<ValidationException> ExceptionsList
        {
            get { return exceptionDictionary.Values; }
        }

        /// <summary>
        /// Constructor for the Validation Exception Manager
        /// </summary>
        /// <param name="packagePath">Path that contains the exception file.</param>
        public ValidationExceptionManager(string packagePath)
        {
            exceptionDictionary = new Dictionary<string, ValidationException>();
            var filePath = Path.Combine(packagePath, ValidationExceptionsFileName);

            if (File.Exists(filePath))
            {
                ValidationExceptions exceptions = Utilities.GetDataFromJson<ValidationExceptions>(filePath);
                exceptionDictionary = exceptions.Exceptions.ToDictionary(ex => ex.ExceptionError);
            }
            else
            {
                exceptionDictionary = new Dictionary<string, ValidationException>();
            }
        }

        /// <summary>
        /// Tests whether the requested error is part of the validation exception list.
        /// </summary>
        /// <param name="validationTest">Validation test display name</param>
        /// <param name="validationError">Error string, verbatim</param>
        /// <returns>True if the error is part of the validation exception list.</returns>
        public bool IsException(string validationTest, string validationError, string packageVersion)
        {
            ValidationException validationException;
            if (exceptionDictionary.TryGetValue(validationError, out validationException))
            {
                if (validationException.ValidationTest == validationTest &&
                    validationException.PackageVersion == packageVersion)
                {
                    // Flag that this exception was found
                    validationException.Used = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks the validity of the validation exception list.
        /// </summary>
        /// <param name="packageVersion">Package version used for comparison.</param>
        /// <returns> Returns a list of issues found with the exception list.  That list will be empty when no issues are found.</returns>
        public IEnumerable<string> CheckValidationExceptions(string packageVersion)
        {
            List<string> issuesList = new List<string>();
            foreach (var validationException in ExceptionsList)
            {
                // Validate that all exceptions that were used had the right package version.
                if (validationException.PackageVersion != packageVersion)
                {
                    issuesList.Add(string.Format("The following error was tagged as an exception to validation, but for a previous version of the package.  Please consider getting this exception fixed and removed from \"ValidationExceptions.json\" before updating its package version field.\r\n    \"{0}\" - \"{1}\"", validationException.ValidationTest, validationException.ExceptionError));
                }
                // Validate that there were no unused exceptions.
                else if (!validationException.Used)
                {
                    issuesList.Add(string.Format("The following exception was listed, but not used during validation.  Please remove it from \"ValidationExceptions.json\":\r\n    \"{0}\" - \"{1}\"", validationException.ValidationTest, validationException.ExceptionError));
                }
            }

            return issuesList;
        }
    }
}
