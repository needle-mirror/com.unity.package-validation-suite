# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.73.0-preview] - 2024-05-23
- PVP: Add x-ray check `PVP-164-1` flagging nonexistent Unity min-versions. (PVS-202)

## [0.72.0-preview] - 2024-05-15
- Improved error message when ValidationExceptions.json contains unsupported mutually exclusive validation exception entries. (PVS-198)

## [0.71.0-preview] - 2024-05-14
- PVP: Remove x-ray checks `PVP-{120,121,122,123,124,125}-1`, obsolete since v0.56.0-preview.
- PVP: Fix `PVP-26-3` crash in packages with case collisions in paths.
- PVP: Add x-ray check `PVP-26-4`, addressing several bugs in `PVP-26-3`.
- PVP: Add x-ray check `PVP-39-1` to catch Git conflict markers in text files. (PETS-1484)
- PVP: Add x-ray check `PVP-111-2` with stricter .repository.url validation. (PVS-196)
- PVP: Add x-ray check `PVP-114-1` checking package .name and .type consistency. (PVS-204)
- PVP: Add x-ray check `PVP-200-1` flagging unit tests in a non-test package. (PVS-201)
- PVP: Determine _publish set_ (formerly _verification set_) from `pvpPublishSet` property in project manifest, if specified. (PETS-1481)
- PVP: Only run PVP checks for _packages under test_, given by `pvpUnderTest` property in project manifest, if specified. (PETS-1481)

## [0.70.0-preview] - 2024-03-07
- Add PVP-102-3, relaxing ".unity" manifest property validation for Unity 6.
- Fix bugs around handling Unity 6 version number.

## [0.69.0-preview] - 2024-03-04
- Allow Burst lld binaries for Windows Arm64, e.g. burst-lld-16-hostwin-arm64.exe.

## [0.68.0-preview] - 2024-02-23
- PvpXray: Fix partially read HTTP response body when body is gzip/deflate compressed in some versions of Unity (all platforms).

## [0.67.0-preview] - 2024-02-20
- Remove dependency on log4net.

## [0.66.0-preview] - 2024-02-14
- Virtual paths in `APIDocumentationIncludedUS0041` are now resolved using `FileUtil.GetPhysicalPath`

## [0.65.0-preview] - 2024-02-05
- PVP: Add non-xray check `PVP-163-1` to validate `mustBeBundled` property in editor manifest. (PVS-195)

## [0.64.0-preview] - 2024-01-25
- PvpXray: Fix partially read HTTP response body on Windows when body is gzip/deflate compressed. (PETS-1462)

## [0.63.0-preview] - 2024-01-15
- PVP: Add x-ray check `PVP-38-1` to catch use of Resources system directory. (PVS-194)

## [0.62.0-preview] - 2023-12-05
- PVP: Remove old `PVP-{90,91,92}-1` NDA checks in favor of new NDA checks in upm-pvp. (PETS-1412)

## [0.61.0-preview] - 2023-10-30
- PVP: Performance improvements. (PETS-1424)
- PVP: Remove PVP-26-{1,2} which were obsoleted in 0.50.0-preview and performed poorly. (PETS-1424)
- PVP: Add x-ray check `PVP-160-1` to catch unavailable direct package dependencies. (PVS-171)
- PVP: Cache HTTP requests made by x-ray checkers.
- PVP: Add x-ray checks `PVP-{171,173,181}-1, catching incompatible changes to the package Unity min-version and dependencies. (PVS-189)
- PVP: Add x-ray check iteration `PVP-106-2` that additionally disallows version 0.0.0 and missing version field. (PETS-1420)

## [0.60.0-preview] - 2023-10-13
- PVP: Add x-ray checks `PVP-{161,162}-1` to catch certain package dependency errors. (PVS-188)
- PVP: Now supports Unity 2018.4 or later (previously only supported 2019.2+).
- PVP: Work around Packman issue causing spurious `_fingerprint` errors in PVP-107-2. (PVS-1414)

## [0.59.0-preview] - 2023-09-05
- PVP: Add x-ray check `PVP-93-1` to verify that there are no files greater than 1 GB (1e9 bytes). (PVS-187)

## [0.58.0-preview] - 2023-09-04
- PVP: Add x-ray checks `PVP-{33,34}-1` to verify that there are no filenames that ought to never appear in a package. (PVS-186)
- PVP: Add x-ray check `PVP-{35,82}-1` to verify that there are no unmodified Packager Starter Kit files. (PVS-186)
- PVP: Add x-ray check `PVP-36-1` to verify that there are no Git LFS pointer files.
- PVP: Add x-ray check `PVP-37-1` to verify that there are no Unity assets using binary serialization (PETS-1381).

## [0.57.0-preview] - 2023-08-22
- Fix invocation of API updater for certain Unity branches. (PVS-185)
- PVP: Add x-ray check `PVP-155-1` to verify that `Documentation~/filter.yml` is valid. (PVS-184)
- PVP: Add x-ray check `PVP-156-1` to verify that `Documentation~/filter.yml` uses sensible regexes. (PVS-184)
- PVP: Add x-ray check `PVP-157-1` to verify that `Documentation~/filter.yml` contains no non-standard rules. (PVS-184)

## [0.56.0-preview] - 2023-08-16
- PVP: Add x-ray check `PVP-73-1`, checking that filename extensions use correct casing.
- PVP: Add x-ray checks `PVP-{120,121,122,123,124,125}-2` to verify that even more text files are well-formed. (PVS-182)

## [0.55.0-preview] - 2023-07-17
- Enable HTTP response compression when fetching files over HTTP. (PETS-1367)
- PVP: Add x-ray check `PVP-100-2`, doing strict UTF-8 validation of the manifest (which `PVP-100-1` doesn't). (PVS-182)

## [0.54.0-preview] - 2023-05-12
- PVP: Add x-ray checks `PVP-{90,91,92}-1` for NDA validation. (PETS-1221)
- PVP: Add x-ray checks `PVP-{120,121,122,123,124,125}-1` to verify that text files are well-formed. (PVS-154)

## [0.53.0-preview] - 2023-05-04
- Relax "Package Lifecycle Validation" to always allow version numbers with major version zero without a pre-release version. (PVP-178)
- Include `PVP-106-1` (Version tag complies with Package Lifecycle rules) in "X-ray Validations" validation. (PVS-178)

## [0.52.0-preview] - 2023-05-01
- PVP: Add x-ray check `PVP-106-1` to verify package lifecycle. (PVS-132)

## [0.51.0-preview] - 2023-04-27
- PVP: Add x-ray checks `PVP-130-1`, `PVP-131-1`, `PVP-132-1`, and `PVP-133-1` to verify assembly definition files. (PVS-113)
- PVP: Add check `PVP-140-1` that wraps `UpdateConfigurationValidation`. (PVS-180)

## [0.50.0-preview] - 2023-04-18
- PVP: Add x-ray checks `PVP-70-1`, `PVP-71-1` and `PVP-72-1` to verify package filenames. (PVS-176)
- MetaFilesValidation: Don't check for Meta files inside Loadable Plugin Directories. (PVS-179, UUM-9421)
- PVP: Added new x-ray check `PVP-26-3` for meta file validation, exempting Loadable Plugin Directories and fixing a bug in the previous `PVP-26-2` check. (PVS-179, UUM-9421)

## [0.49.0-preview] - 2023-02-14
- RestrictedFilesValidation: Allow additional Burst DLL files.

## [0.48.0-preview] - 2023-01-17
- SamplesValidation: Allow sample directory without `.sample.json` file. (UPMCI-326)
- SamplesValidation: Ensure samples declared in package manifest are rooted in `Samples~` directory. (UPMCI-326)

## [0.47.0-preview] - 2023-01-13
- PVP: Add x-ray check `PVP-113-1`, checking manifest type attribute for a valid value (PVS-168)

## [0.46.0-preview] - 2023-01-06
- PVP: Add x-ray check `PVP-27-1`, checking for duplicate GUIDs in .meta files. (PVS-162)
- PVP: Add x-ray check `PVP-50-1`, checking for presence of README.md file.

## [0.45.0-preview] - 2022-12-05
- PVP: Add x-ray implementation of manifest file check `PVP-110-1`, banning the `dist` manifest key. (PETS-1173)

## [0.44.0-preview] - 2022-12-02
- PVP: Add `PVP-104-1` that does not allow `displayName` to begin with the string `unity` in the package manifest. (PVS-164)

## [0.43.0-preview] - 2022-12-01
- Includes Data/Tools/Compilation/ApiUpdater folder when probing for APIUpdater.ConfigurationValidator (this is a preparation for the actual move in trunk)

## [0.42.0-preview] - 2022-12-01
- Use new HTTP implementation to download packages from production registry instead of using npm. (PVS-141)
- Remove "Structure" and "Against Asset Store standards" validation type items from dropdown in UI. (PVS-137)
- Change "Against Unity production standards" validation type item always correspond to `ValidationType.Promotion`. It would previously resolve to `ValidationType.LocalDevelopmentInternal` if the package source was not `PackageSource.Registry`. (PVS-137)

## [0.41.0-preview] - 2022-11-30
- PVP: Add `PVP-107-2` that allows a `host` string property in package manifest and remove `PVP-107-1`. (PVS-158)
- Add PvpXray namespace with a number of useful string extension methods.
- Removed references to "Pure File Validation", now instead called "X-ray".
- New, more robust HTTP implementation for querying package registry data. (PVS-160)

## [0.40.0-preview] - 2022-11-24
- PVP: Add x-ray implementation of manifest file PVP checks `PVP-102-2 PVP-103-1 PVP-105-1 PVP-108-1 PVP-111-1 PVP-112-1`. (PVS-134)
- PVP: Add x-ray implementation of documentation PVP checks `PVP-60-1 PVP-61-1`. (PVS-133)

## [0.39.0-preview] - 2022-11-14
- PVP: Add new iteration of meta file x-ray check `PVP-26-2` that ignores contents of hidden directories. (PVS-115)

## [0.38.0-preview] - 2022-11-10
- Disable PVP-107-1 manifest validation check in legacy PVS ("X-ray Validations" validation). (PVS-157)

## [0.37.0-preview] - 2022-11-09
- Don't include meta file check `PVP-26-1` in "X-ray Validations" validation. (PVS-155)

## [0.36.0-preview] - 2022-11-08
- Failed attempt at fixing PVS-155.

## [0.35.0-preview] - 2022-11-08
- Remove legacy placeholder signature validation. (PVS-71)
- PVP: Add x-ray implementation of signature PVP check `PVP-28-1`. (PVS-71)
- PVP: Add x-ray implementation of allowed manifest properties PVP check `PVP-107-1`. Check included in "X-ray Validations" validation. (PVS-115)
- PVP: Add x-ray implementation of meta file PVP check `PVP-26-1`. (PVS-129)
- PVP: Add x-ray implementations of changelog PVP checks `PVP-{40,41,43}-1`. (PVS-130)
- PVP: Add x-ray implementations of license PVP checks `PVP-{30,31,32}-1`. (PVS-131)
- PVP: Change JSON path syntax used in PVP error messages. Object keys not matching regex `^[_a-zA-Z][_a-zA-Z0-9]*$` are now represented as `.["foo.bar"]` instead of `.foo.bar` to avoid ambiguities.

## [0.34.0-preview] - 2022-10-24
- PVP: fix "IPvpChecker added result for undeclared check PVP-80-1" error

## [0.33.0-preview] - 2022-10-24
- PVP: Add x-ray implementations of PVP checks `PVP-{80,81}-1`. (PVS-135)
- Added retry logic and logging to a handful of flaky IO operations. (PVS-122)

## [0.32.0-preview] - 2022-10-19
- Change PVP result file extension from `.json` to `.result.json`. (PETS-1067)

## [0.31.0-preview] - 2022-09-22
- Updated [Unity.APIValidation](https://github.cds.internal.unity3d.com/unity/Unity.APIValidation/commit/569110df518f678d09e8b16fcc81e0a45862e197)
    - Fixed generic instance types with partial matching type argument list reporting to be equal.

## [0.30.0-preview] - 2022-09-21
- Add new check to catch `index.md` documentation files with wrong filename casing. (PVS-121)
- PVP: Add "X-ray" validation framework and a number of new PVP checks: `PVP-{21,22,23,24,25,62,100,101,102}-1`. (PVS-128)
- Allow the new Burst LLD wrapper executable to pass package validation.
- Fixed ApiValidation and UpdateConfigurationValidation potentially validating against assemblies of unrelated package if package name is a prefix of another package name. (PETS-1043)

## [0.29.0-preview] - 2022-09-09
- [Unity.APIValidation](https://github.cds.internal.unity3d.com/unity/Unity.APIValidation/commit/9ff008f8c458bbc680c94e32544ff3eeca595f6f)
    - Fixed comparison of members containing types with modifiers
    - Fixed crash when validating property setter overriding
    - Fixed potential failure to resolve overriden method when its return type is a type parameter.

## [0.28.0-preview] - 2022-09-08
- Relax Samples test to allow users to have a `Samples~` folder without a `.sample.json` file for VerifiedSet test run in APV

## [0.27.0-preview] - 2022-08-18
- [Unity.APIValidation](https://github.cds.internal.unity3d.com/unity/Unity.APIValidation/commit/a84299c8e32ae068b765a298f0f2483541e270c9): [do not crash upon failure to resolve attributes](https://github.cds.internal.unity3d.com/unity/Unity.APIValidation/commit/3bc5e3c704c54f06ed4307426884e6b5b7db7e66)

## [0.26.0-preview] - 2022-07-29
- XmlDocValidation: improved diagnostics when FindMissingDocs.exe fails to execute.
- RestrictedFilesValidation: Allow files named bee.bat and bee.ps1.

## [0.25.0-preview] - 2022-07-12

## [0.24.0-preview] - 2022-06-28
- Remove repository metadata checks in Manifest Validation.

## [0.23.8-preview] - 2022-05-20
- Renamed log4net assembly to avoid name clash with other packages.
- Package Verification Profiles: added `Pvp.RunTests` entrypoint and support for running API docs validation in PVP mode.
- Adjust validation of `documentationUrl` package manifest property to prepare for UPMCI-174. (This does not affect package authors.)

## [0.23.7-preview] - 2022-03-17
- Added [Unity.APIValidation](https://github.cds.internal.unity3d.com/unity/Unity.APIValidation) as a submodule to remove dependency on specific version of Mono.Cecil
- Template project manifest validation: Fixed false errors when running PVS manually from within the editor (PVS-82).

## [0.23.6-preview] - 2022-02-23
- Skip a number of tests on Apple silicon (case 1387086)

## [0.23.5-preview] - 2022-01-17
- Fixed reversed comparison direction, missing entry for added directory, and leading slashes in DiffEvaluation report. Also normalized line and path separator to always be a resp. new line and forward slash.

## [0.23.4-preview] - 2021-12-07
- Fixed platform-dependent line-endings in output of UpdateConfigurationValidation.
- Fixed ApiValidation flagging new overrides as additions.
- Changed API Updater Configuration Validation to ignore assemblies that are not part of the final package.
- Introduced a way for devs to bypass ApiValidation sanity checks to be able to run tests locally regardless of OS/Unity version.
- Fixed missing assembly qualification in ApiUpdater Configuration Validator tests.

## [0.23.3-preview] - 2021-11-10
- Fixed `Mono.Cecil.AssemblyResolutionException` while running API Updater Configuration Validation failing to resolve assemblies under `Library/ScriptAssemblies` folder.


## [0.23.2-preview] - 2021-11-02
- Fixed `Mono.Cecil.AssemblyResolutionException` while running API Updater Configuration Validation failing to resolve `UnityEngine.CoreModule` because its location was not included in the search path.

## [0.23.1-preview] - 2021-10-29
- Changed API Updater Configuration Validation to pass a list of folders which ConfigurationValidator tool uses when resolving assemblies.

## [0.23.0-preview] - 2021-10-18
- Fixed API Updater Configuration Validation failing due to passing a too long arguments string to the ConfigurationValidator process.
- Changed API Validation to only run when on Windows and the editor version matches the `unity` property of the package manifest. Previous assemblies are now fetched under a new v2 prefix were all assemblies were built on Windows with an editor version matching the aforementioned `unity` property.

## [0.22.0-preview] - 2021-09-01
- Fixed dead link to license file reference in License Validation warning message.
- Fixed some issues with validations failing due to Windows path length limitation.
- Changed all existing validations to be exemptible with ValidationExceptions.json file.

## [0.21.1-preview] - 2021-08-16
- Fixed License Validation license header check being more strict than US-0032 standard. Year just needs to be a string of digits and entity name just needs to be a non-empty string without trailing spaces.
- Fixed the anchor id in the Manifest Validation documentation page

## [0.21.0] - 2021-07-05
- Added a validation type dropdown button to the ui so that users can choose between one of four validation types: Structure, Asset Store standards, Unity candidates standards, and Unity production standards.
- Added support for DocFX filter.yml in Xmldoc Validation. Exclusion declared in the file Documentation~/filter.yml relative to the package root will be ignored.
- Changed API Validation to fetch assemblies from IT Artifactory instead of decommissioned PRE Artifactory.

## [0.20.2] - 2021-05-17
- The Manifest Validation dependency check is now aware of feature set packages and will behave accordingly by ensuring that all of their dependencies have their version specified as "default".
- Enabling the SamplesValidation for FeatureSets
- Added tests to Templates Validation for ensuring folder and asset names follow the naming conventions, checking for empty directories and validating that the folder hierarchy depth is not exceeded.

## [0.20.1] - 2021-04-26
- Added more descriptive error message when API Validation is unable to compare assemblies.
- Added validation exception helper text after every error with an example of how to add an exception for that error specifically.
- Fixed NullReferenceException error when interacting with Package Manager window.
- Fixed *API Validation* incorrectly reporting members implementing interface members as *virtual*.
- Added initial support for FeatureSets that runs the Changelog and Manifest validation.
- Fixed documentation hyperlinks to be clickable in Yamato UI.

## [0.20.0] - 2021-03-30
- Added validation that ensures type correctness on the fields of package.json
- Fixed crash in *API Validation* when a type contains multiple method overloads which only differ in generic type parameters.

## [0.19.3] - 2021-02-08
- Fixed `XmlDocValixdation` to properly Xml-escape default parameters containing `<` and `>`.
- Added support for excepting Package Unity Version Validation errors.

## [0.19.2] - 2021-01-11
- Fixed Unity Version Validation for versions with dot in version suffix.

## [0.19.1] - 2021-01-06
- Fixed License Validation warning about incorrect format when year is not current. Year should be the year in which the package was first made publicly available under the current licensing agreement.
- Fixed activity log messages to not show stacktrace for a cleaner editor log.

## [0.19.0] - 2020-12-15
- Added support for excepting Asset Validation errors.
- Changed the promoted dependencies check (`ManifestValidation`) to allow un-promoted dependencies if they're included in a list of packageIds provided to the Package Validation Suite
- Fixed `System.NullReferenceException` during `API Validation`.
- Added validation for the RC state (the package is a candidate for that specific editor version). This works only for 2021.1+

## [0.18.1] - 2020-11-17
- Fixed `Editor` namespace clashing with `Editor` type (introduced in 0.18.0).

## [0.18.0] - 2020-11-16
- Added Template Validation that errors when _Enable Preview Packages_ or _Enable Pre-release Packages_ is set in Package Manager settings.
- Moved restricted file extensions `.jpg` and `.jpeg` to new Asset Validation where they are allowed in `Documentation~` and `Tests` directories.
- Added Package Unity Version Validation that errors when the minimum Unity version requirement is made more strict without also bumping (at least) the minor version of the package. A warning is added instead for non-Unity authored packages. In addition, a warning is added when the minimum Unity version requirement is made less strict.
- Fixed compilation warnings.
- Changed the `XmlDocValidation` test warning to an error when the underlying tool `FindMissingDocs` fails by throwing unhandled exceptions.
- Fixed `XmlDocValidation` support for `CDATA` sections in Xml documentation.

## [0.17.0] - 2020-10-05
- Changed the Release Validation into a warning instead of an Error
- Added primed library validation which makes sure templates are packed with their Library folder for speedier creation of projects using those templates
- Added Author field validation

## [0.16.0] - 2020-09-16
- Fixed System.IO.DirectoryNotFoundException which can occur if Logs folder is missing
- Fixed Validation Suite tests to succeed when exceptions are used despite errors being thrown
- Added validation to prevent too big gaps in package versions relative to the previous one
- Added template validation that errors when not allowed fields are used in the template project manifest

## [0.15.0] - 2020-09-01
- Fixed dependency check not being run for Lifecycle V1 validations
- Enabled Lifecycle V2 checks

## [0.14.0] - 2020-08-10
- Changed LifecycleValidationV1 to support 6 digit version number for preview packages.
- Added mechanism to execute tests on specific package types
- Added internal exceptions for Roslyn binaries

## [0.13.0] - 2020-06-10
- Fixed System.InvalidCastException during "API Validation"
- Fixed System.NullReferenceException at Unity.APIComparison.Framework.CecilExtensions.IsPublicAPI() during "API Validation"
- Fixed false positive breaking change when making property accessor more accessible
- Fixed the validation of dependencies to look for the version chosen by the resolver instead of the verbatim version
- Added logging to the validation suite steps
- Changed the exception mechanism to make an exception for the whole validation category
- Changed the exception mechanism to not fail on unused exception rules anymore

## [0.11.0] - 2020-05-26
- Added new rule where the first pre-release version of a package must be promoted by release management
- Added new rule where the package iteration must be higher than the nighest published iteration
- Added new rule where release versions of a package must be promoted by release management
- Added new rule where the very first version of a package must be promoted by release management
- Added new rule validating the unity and unityRelease fields of a package manifest
- Added the exception mechanism to the restricted files validation

## [0.10.0] - 2020-05-05
- Added a new Promotion context and transferred all the Publishing tests to this new context
- Added new rule where a package name cannot end with .plugin, .framework or .bundle
- Added new rule where a package should not include documentationUrl field in the manifest
- Added csc.rsp to the list of restricted files

## [0.9.1] - 2020-03-25
- Fixed unused variable in LifecycleValidation exception block

## [0.9.0] - 2020-03-24
- Added new rules to validate version tagging in lifecycle v2
- Added Validation Exception mechanism to be able to manage known exceptions
- Added whitelist for dsymutil.exe. Required to support debug symbols for MacOS cross compilation
- Added profile markers to Validation Suite tests
- Changed Lifecycle V2 version validation to 2021.1

## [0.8.2] - 2020-03-03
- Changed validation to warning when License is not present for Verified packages

## [0.8.1] - 2020-02-20
- Whitelisted bee.dll, pram.exe, tundra2.exe. Required for incremental build pipeline used in com.unity.platforms
- Added information line to API Validation to know which version was used for comparison
- Fixed validate button not appearing when a package was added through a git URL or a local tarball path

## [0.8.0] - 2020-02-04
- Added error to fail validation if a package has unity field 2020.2 until the new Package Lifecycle is ready
- Added error when UNRELEASED section is present in a package CHANGELOG.md during promotion
- Added warning when UNRELEASED section is present in a package CHANGELOG.md during CI/publish to candidates
- Changed display name validation to allow up to 50 characters instead of 25
- Changed path length validation to ignore hidden directories
- Fixed documentation generation errors (missing index.md file)

## [0.7.15] - 2020-01-22
- Added Validation Suite version to the validation suite text report
- Added support of <inheritdoc/> tag in missing docs validation.
- Fixed issue with API Validation not finding some compiled assemblies inside a package

## [0.7.14] - 2020-01-03
- Whitelisting ILSpy.exe

## [0.7.13] - 2019-12-16
- Whitelisting Unity.ProcessServer.exe

## [0.7.12] - 2019-12-09
- Fix Assembly Validation to better distinguish test assemblies.

## [0.7.11] - 2019-11-28
- Made changes to allow running Validation Tests from other packages.
- Bug fixes in License Validation.
- Bug fixes in UpdaterConfiguration Validation.

## [0.7.10] - 2019-11-01
- Fix an issue with the restricted file validation

## [0.7.9] - 2019-10-31
- Happy Halloween!!
- Relaxed the API validation rules in preview
- Added a more restrictive forbidden files list.

## [0.7.8] - 2019-10-17
- Removed Dependency Validation check
- Added "com.ptc" as a supported package name domain.

## [0.7.7] - 2019-09-20
- Added whitelist for HavokVisualDebugger.exe

## [0.7.6] - 2019-09-19
- Fix bug preventing the Validation Suite from properly running against latest version of Unity.

## [0.7.5] - 2019-09-18
- Fixed issue causing built-in packages validation to fail in Unity versions < 2019.2

## [0.7.4] - 2019-09-16
- Disable semver validation upon api breaking changes on Unity 2018.X
- Allow console error whitelisting for API Updater Validation.

## [0.7.3] - 2019-09-10
- Removed Dependency Validation test to prevent asking for major version changes when adding or removing dependencies
- Fixed issues with scope of references used in APIValidation Assembly

## [0.7.2] - 2019-08-27
- Add support for 2018.3 (without the package manager UI integration).

## [0.7.1] - 2019-08-23
- Modified the test output structure to differentiate info, warning and error output.
- Added validation test to check for the existing of the "Resources" directory in packages, which is not recommended.
- Modified Packman UI integration to turn yellow upon warnings in a run.
- Fixed preview package fetch, to allow API evaluation testing, as well as diff generation.

## [0.6.2] - 2019-07-15
- Allows validation suite to be used by develop package
- Moved validation suite output to Library path

## [0.6.1] - 2019-07-15
- Changed maximum file path length validation to be 140 characters instead of 100.
- Changed Dependency Validation to issue a Warning instead of an error when package's Major version conflicts with verified set.
- Added exception handling in BuildTestSuite when calling assembly.GetTypes()
- Fixed path length validation to handle absolute/relative paths correctly

## [0.6.0] - 2019-07-11
- Added Maximum Path Length validation to raise an error if file paths in a package are becoming too long, risking Windows long path issues to appear.
- Fixed another issue in UpdateConfiguration validation causing some false-positives in DOTS packages.

## [0.5.2] - 2019-05-17
- removing validations involving where tests should be found.  They can now be anywhere.

## [0.5.1] - 2019-05-17
- Patched an issue in the UpdateConfiguration validation

## [0.5.0] - 2019-05-15
- Added XML Documentation validation
- Added ApiScraper exception to RestrictedFilesValidation
- Changed outdated documentation

## [0.4.0] - 2019-04-03
- Properly handle dependencies on built-in packages, which aren't in the production registry.
- Fix unit tests
- Added support for local validation of packages with unpublished dependencies.
- Add new public API to test all embedded packages.
- Validate that package dependencies won't cause major conflicts
- Validate that package has a minimum set of tests.
- Fix the fact that validation suite will pollute the project.
- Add project template support
- Hide npm pop-ups on Windows.
- Fix validation suite freeze issues when used offline
- Add validation to check repository information in `package.json`
- Validate that both preview and verified packages have their required documentation.
- Refactor unit tests to use parametrized arguments.
- Support UI Element out of experimental
- Added support for validating packages' local dependencies during Local Development
- Removed ProjectTemplateValidation test
- Add validation to check that API Updater configurations are not added outside major releases.
- Add unit tests to Unity Version Validation
- Fixing bug PAI-637 : searches for word "test" in path and finds it in file name rather than searching only folder names.

## [0.3.0] - 2018-06-05
- Hide validation suite when packages are not available
- Accept versions with and without  pre-release tag in changelog
- Fix 'View Results' button to show up after validation
- Shorten assembly definition log by shortening the path
- Fix validation of Assembly Definition file to accept 'Editor' platform type.
- Fix npm launch in paths with spaces
- Fix validation suite UI to show up after new installation.
- Fix validation suite to support `documentation` folder containing the special characters `.` or `~`
- Fix validation suite display in built-in packages
- Add tests for SemVer rules defined in [Semantic Versioning in Packages](https://confluence.hq.unity3d.com/display/PAK/Semantic+Versioning+in+Packages)
- Add minimal documentation.
- Enable API Validation
- Clarify the log message created when the old binaries are not present on Artifactory
- Fix build on 2018.1

## [0.1.0] - 2017-12-20
### This is the first release of *Unity Package Validation Suite*.
