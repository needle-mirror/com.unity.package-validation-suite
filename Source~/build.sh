#!/bin/bash
set -ex
CONFIG=Release
DEBUGTYPE=None

echo "Building XmlDoc..."
pushd XmlDoc
nuget restore
msbuild /p:Configuration=$CONFIG \
	/p:AllowedReferenceRelatedFileExtensions="-" \
	/p:DebugType=$DEBUGTYPE \
	/t:Build \
	Unity.XmlDoc.FindMissingDocs.Driver/Unity.XmlDoc.FindMissingDocs.Driver.csproj
popd

echo "Overwriting new version of XmlDoc ..."
rm -rf ../Bin\~/FindMissingDocs
cp -r XmlDoc/Unity.XmlDoc.FindMissingDocs.Driver/bin/$CONFIG ../Bin\~/FindMissingDocs
