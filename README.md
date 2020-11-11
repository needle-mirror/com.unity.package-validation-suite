# Unity Package Validation Suite

Unity Package Validation Suite, used in conjunction with the Package Manager
window to enable the evaluation of a package's integrity before it is
considered for publishing.

## CI Status

[![](https://badges.cds.internal.unity3d.com/packages/com.unity.package-validation-suite/build-badge.svg?branch=dev)](https://badges.cds.internal.unity3d.com/packages/com.unity.package-validation-suite/build-info?branch=dev)

## Public Package Versions

[![ReleaseBadge](https://badges.cds.internal.unity3d.com/packages/com.unity.package-validation-suite/release-badge.svg)]()
[![ReleaseBadge](https://badges.cds.internal.unity3d.com/packages/com.unity.package-validation-suite/candidates-badge.svg)]()

## Before opening the project

This repository uses git submodules. Make sure you run these before opening the
package in Unity Editor.

```sh
git submodule sync --recursive
git submodule update --init --recursive
git submodule foreach git lfs pull
```

*Note:*
The `--init` argument for git submodule update is only needed the first time
opening the project on your machine. Subsequent runs can drop this flag.

The project contains large files that requires you to install git [Large File
Storage][1]. Download and run the installer on your machine then run:

```sh
git lfs install
```

[1]: https://git-lfs.github.com/
