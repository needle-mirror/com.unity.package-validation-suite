# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
