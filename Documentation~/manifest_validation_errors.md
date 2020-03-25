# Manifest Validation Errors

Manifest validations attempt to detect issues created by an incorrect manifest setup, as well as gate publishing of packages that do not conform to Unity standards.

## Manifest not available. Not validating manifest contents
The validated package does not seem to contain a `package.json` file.

## version needs to be a valid Semver
The value of the `version` field in the package.json file does not contain a valid Semver value. A valid Semver value follows the format of x.y.z[-tag], with the *-tag* section being optional.
At Unity, we have specific uses for the *-tag* section, rendering the use of Semver a bit more restricted:

Examples of valid Unity Semver in Lifecycle V1:
* 0.0.1
* 1.0.0
* 1.0.0-preview
* 1.0.0-preview.1

Examples of valid Unity Semver in Lifecycle V2:
* 0.0.1
* 1.0.0
* 1.0.0-exp.1
* 1.0.0-exp-feature.1
* 1.0.0-pre.1
* 1.0.0-rc.1

## dependency needs to be a valid Semver
In package.json, the specified dependency does not have a valid Semver value. For more information on valid Semver values please refer to [version needs to be a valid Semver](#version-needs-to-be-a-valid-semver)

## Package dependency [packageID] is not published in production
The specified dependency does not exist in the production registry.

## name needs to start with one of these approved company names
The `name` value in package.json needs to start with one of the approved namespaces for Unity packages.

Current approved list:
* com.unity.
* com.autodesk.
* com.havok.
* com.ptc.

## name is not a valid name
The `name` value in package.json does not conform to Unity requirements. Unity requires that a package name meets the following requirements:

* Only lowercase letters and numbers
* No whitespaces
* No special characters other than `-`, `.` or `_`

This is validated through the following regular expression: `^[a-z0-9][a-z0-9-._]{0,213}$`

## name cannot contain capital letters
The `name` value in package.json can only contain lowercase letters. For additional requirements of the name field, refer to ["name" is not a valid name](#"name"-is-not-a-valid-name)

## displayName must be set
The `displayName` field in package.json must have a value

## displayName is too long
The `displayName` field in package.json is too long

## displayName cannot have any special characters
The `displayName` field in package.json can contain only the following characters:

* Letters
* Numbers
* White spaces

This is validated through the following regular expression: `^[a-zA-Z0-9 ]+$`

## description is too short
The `description` field is too short. This field needs to contain relevant information about the package since it is presented in the UI to the user.

## for a published package there must be a repository.url field
For packages that are published to the public registry, a `repository.url` field needs to exist in package.json to make it easier to identify where it came from

## for a published package there must be a repository.revision field
For packages that are published to the public registry, a `repository.revision` field needs to exist in package.json to make it easier to identify on what specific commit a package was published