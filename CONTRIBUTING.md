# To Release a new version of Validation Suite

## The Process   

1. Branch off from dev and name it `release/X.Y.Z`  
1. Update package.json version  
1. Make sure CHANGELOG is accurate, add date  
1. Review the README and document what is missing, if needed  
1. Review the yamato files to make sure we are testing against relevant unity versions  
1. Create PR to merge `release/X.Y.Z` to `dev`  
    1. The tests will run automatically (Tests Trigger, which triggers testing on windows and mac from 2018.4 to trunk)  
    2. You can pre-emptively and manually run the promotion tests here
1. Once merged to dev, merge `release/X.Y.Z` to `master` (no PR required)  
1. Publish master by tagging the repo with X-Y-Z to trigger the job named `Automated Trunk PR`.  
1. Publish a message on [#devs-packman-tooling](https://unity.slack.com/archives/C26EP4SUQ)  announcing the new version release. See [Template message](#template-message)  - Release Message  
At this point, upm-ci will have two "live" versions: CI and trunk  
1. Follow up on the ono PR. Once it gets merged in trunk, publish a message on [#devs-packman-tooling](https://unity.slack.com/archives/C26EP4SUQ) announcing the new version release. See [Template message](#template-message) - Release Message 
At this point, both trunk and upm-ci will have the same "live" version.  

## Template message  

@here We have released version `<version>` of the `Validation Suite`. This version contains the following changes:  
```  
<changes>  
```  
*Current Live Versions:*  
CI: <latest version on prod>  
trunk: <latest version on trunk>  