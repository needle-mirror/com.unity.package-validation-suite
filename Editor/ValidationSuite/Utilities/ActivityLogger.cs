using System;
using UnityEngine;

/// <summary>
/// Logger used to keep track of Validation Suite activity, for debugging
/// </summary>
internal class ActivityLogger
{
    public static void Log(string message, params object[] args)
    {
        var finalMessage = "Package Validation Suite: " + string.Format(message, args);

        Debug.Log(finalMessage);
    }
}
