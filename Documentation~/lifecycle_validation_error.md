# Lifecycle Validation error

Here you can read more information about the Lifecycle validation errors that the Validation Suite can produce, how to solve them and where to read more about how to solve it.

## "version" needs to be a valid "Semver"

The version you have specified doesn't follow correct [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## "version" doesn't follow our lifecycle rules. "tag" is invalid.

The version you specified does not conform to our tagging rules, for either Lifecycle v1 or v2 (2020.2+).

## Lifecycle V1 Tagging Rules

### Core packages cannot be preview.

This package is considered as part of Unity "core" and cannot be tagged with "-preview".

### "version" < 1, please tag the package as XXX-preview

Your version is less than 1.0.0, making it a preview package by default, so please tag is as x.y.z-preview.

### "version": the only pre-release filter supported is "-preview.[num < 999]"

Your version tag is invalid, we only allow x.y.z-preview packages in lifecycle v1.

## 2020.2 Packages are not supported yet!

We are working on transitioning to the package lifecycle version 2 in 2020.2, and the minimum required parts aren't ready.  Until further notice, please ensure the unity field in your package's package.json file is less than 2020.2.

## Lifecycle V2 Tagging Rules

### In package.json, "version" cannot be tagged "preview" in lifecycle v2, please use "exp".

The `-preview` tag has been deprecated in lifecycle v2. If your version is less than `1.0.0`, simply drop the `-preview` tag entirely, otherwise please tag your release using `-exp.1`.

### In package.json, "version" cannot be tagged "XXX" while the major version less than 1.

Your package version is less than `1.0.0`, meaning it is "experimental". Therefore, you cannot use any tags.

### In package.json, "version" must be a valid tag. "XXX" is invalid, try either "pre", "rc" or "exp".

Your package version doesn't match the allowed tags of "pre", "rc" or "exp".

### In package.json, "version" must be a valid tag. Custom tag "XXX" only allowed with \"exp\".

You cannot specify a custom tag on anything but an "exp" type version.

### In package.json, "version" must be a valid tag. Custom tag "XXXXXXXXXXX" is too long, must be 10 characters long or less.

Your custom version tag is too long, please use something 10 characters or less.

### In package, "version" must be a valid tag. Iteration is required to be 1 or greater.

Your version tag requires an iteration number, and it should be 1 or higher (ex. "1.0.0-pre.**1**").

## Package depends on a package which is in an invalid track for release purposes
The package you validated contains dependencies on a different release track. All packages supporting 2020.2+ need to adhere to Lifecycle V2 rules, which means that packages cannot have dependencies in versions that precede their current release track.

**Release Packages**

*Release* (x.y.z) packages can only depend on other *Release* packages (x.y.z)

**Release Candidate Packages**

Release Candidates (x.y.z-rc.n) can depend on:
* Other *Release Candidate* (x.y.z-rc.n)
* A *Release* package (x.y.z)

**Pre Release Packages**

*Pre Release* (x.y.z-pre.n) can depend on:
* Other *Pre Release* (x.y.z-pre.n)
* A *Release Candidate* (x.y.z-rc.n)
* A *Release* package (x.y.z)

**Experimental Packages**

*Experimental* Packages can depend on:
* Other Experimental (0.y.z or x.y.z-exp.n)
* A *Pre Release* (x.y.z-pre.n)
* A *Release Candidate* (x.y.z-rc.n)
* A *Release* (x.y.z)