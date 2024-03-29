# Used by PVS - Package Validation Suite

> Never change the submodule in PVS directly, instead, update the original code from [git](git@github.cds.internal.unity3d.com:unity/Unity.APIValidation.git)

This specific branch is used by PVS to compare assemblies in the semantic version validation (com.unity.package-validation-suite/Editor/ValidationSuite/ValidationTests/ApiValidation.cs)

We are doing that to remove the dependency on a specific Mono.Cecil version so we can update the version shipped with Editor without causing any issues to
the comparison code.

Ideally we would be able to simply add this repo as a submodule, without any changes in PVS but that is not possible due to:
    1. Unity does not support `.csproj` projects so we need to add an `.asmdef`
    2. Unity plugin importer (at least in 2022.2) ignores folders ending with
       `.Framework` (and a couple of other suffixes) so we need to renamed
       `Unity.APIComparison.Framework` to `UnityAPIComparisonFramework`)
## How to fix issues in Unity.APIComparison

> Any new files that are not removed by `script/prepare_pvs.ps1` need to include a respective  `.meta` file.
> Easiest way to generate those is to create an empty Unity project and drop the files in `Assets/` and
> then copy the resulting `.meta` files back.

If we need to fix bugs:

- On Unity.APIValidation working folder (pulled from link above)
  - Update local `master` branch
  - Open a new branch, add your fixes (with tests ;) ) and get them merged to `master` through a PR on github
  - Checkout `pvs_vendoring` branch
  - Revert last commit (`git revert HEAD`) generated by `script/prepare_pvs.ps1`
  - Merge `master` into it, i.e, `git reset --hard && git merge master`
  - Run `prepare_pvs.ps1` (which will remove some files from repo and commit the changes)
    - on linux you may need to install [powershell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux?view=powershell-7.2)
  - Push the newly changeset.
  
- On PVS
  - Pull updates from remote
  - create a branch
  - run `git submodule update --remote com.unity.package-validation-suite/Editor/External/Unity.APIValidation`
  - run `git add com.unity.package-validation-suite/Editor/External/Unity.APIValidation`
  - commit
  - Run tests
  - Follow normal PR process
