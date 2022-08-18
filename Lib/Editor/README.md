# log4net rename

## Problem

1. [Unity.APIValidation](../../Editor/External/Unity.APIValidation/UnityAPIComparisonFramework/Unity.APIComparison.Framework.asmdef) depends on log4net.
2. Multiple packages depending on different versions of log4net causes issues in Unity (this has been fixed in 2022.2 but will not be backported since it is a breaking change)

## Solution

We had 2 options here:

1. Introduce a Unity package for log4net
2. Rename log4net used by PVS

we decided to go for *2* since it requires less work and also is more flexible.
If we ever need to update log4net we may consider introducing a package (option *1*)

## How to do the rename

We use [dotnet alias](https://github.com/getsentry/dotnet-assembly-alias) tool to achieve that.
