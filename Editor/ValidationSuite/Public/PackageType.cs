using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityEditor.PackageManager.ValidationSuite
{
    /// <summary>
    /// The type of packages supported in Validation.
    /// </summary>
    public enum PackageType
    {
        /// <summary>Package containing editor and runtime code to be used within the Unity Editor.</summary>
        Tooling,
        
        /// <summary>Package containing a Unity Template</summary>
        Template,
    }
}
