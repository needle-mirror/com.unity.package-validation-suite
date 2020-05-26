using System;
using System.Collections.Generic;

namespace UnityEditor.PackageManager.ValidationSuite
{
    [Serializable]
    internal class ValidationException
    {
        /// <summary>
        /// Name of validaiton test in which the exception is requested.
        /// </summary>
        public string ValidationTest;

        /// <summary>
        /// Error for which the exception is requested.
        /// </summary>
        public string ExceptionError;

        /// <summary>
        /// Package Version
        /// </summary>
        public string PackageVersion;

        /// <summary>
        /// Internal value to keep track whether exception was used or not
        /// </summary>
        public bool Used;
    }

    [Serializable]
    internal class ValidationExceptions
    {
        public ValidationException[] Exceptions;
    }
}
