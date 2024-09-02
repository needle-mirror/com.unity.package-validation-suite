using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Requirement = PvpXray.ManifestVerifier.Requirement;

namespace PvpXray
{
    class ManifestTypeVerifierV2 : Verifier.IChecker
    {
        public static string[] Checks { get; } = { "PVP-107-2" };
        public static int PassCount => 0;

        static readonly Requirement IsBoolean = new Requirement("must be a boolean", json => json.IsBoolean);
        static readonly Requirement IsNumber = new Requirement("must be a number", json => json.IsNumber);
        static readonly Requirement IsString = new Requirement("must be a string", json => json.IsString);
        static readonly Requirement IsArrayOfString = new Requirement("must be an array of string", json => json.IsArray && json.Elements.All(element => element.IsString));
        static readonly Requirement IsObjectOfString = new Requirement("must be an object with string values", json => json.IsObject && json.Members.All(element => element.IsString));
        static readonly Requirement SamplesRequirement = new Requirement(
            "must be an array of objects with allowed string entries \"description\", \"displayName\", and \"path\" and allowed boolean entry \"interactiveImport\"",
            json => json.IsArray && json.Elements.All(element =>
                element.IsObject && element.Members.All(member =>
                    new[] { "description", "displayName", "interactiveImport", "path" }.Contains(member.Key)
                    && (member.Key == "interactiveImport" ? member.IsBoolean : member.IsString)))
        );

        static readonly (string[], Requirement)[] k_AllowedProperties =
        {
            (new[] { "_upm", "changelog" }, IsString),
            (new[] { "_upm", "gameService", "configurePath" }, IsString),
            (new[] { "_upm", "gameService", "genericDashboardUrl" }, IsString),
            (new[] { "_upm", "gameService", "groupIndex" }, IsNumber),
            (new[] { "_upm", "gameService", "groupName" }, IsString),
            (new[] { "_upm", "gameService", "projectDashboardUrl" }, IsString),
            (new[] { "_upm", "gameService", "projectDashboardUrlType" }, IsString),
            (new[] { "_upm", "gameService", "useCasesUrl" }, IsString),
            (new[] { "_upm", "supportedPlatforms" }, IsArrayOfString),
            (new[] { "author", "email" }, IsString),
            (new[] { "author", "name" }, IsString),
            (new[] { "author", "url" }, IsString),
            (new[] { "category" }, IsString),
            (new[] { "changelogUrl" }, IsString),
            (new[] { "dependencies" }, IsObjectOfString),
            (new[] { "description" }, IsString),
            (new[] { "displayName" }, IsString),
            (new[] { "documentationUrl" }, IsString),
            (new[] { "files" }, IsArrayOfString),
            (new[] { "hideInEditor" }, IsBoolean),
            (new[] { "host" }, IsString),
            (new[] { "keywords" }, IsArrayOfString),
            (new[] { "license" }, IsString),
            (new[] { "name" }, IsString),
            (new[] { "relatedPackages" }, IsObjectOfString),
            (new[] { "repository", "footprint" }, IsString),
            (new[] { "repository", "revision" }, IsString),
            (new[] { "repository", "type" }, IsString),
            (new[] { "repository", "url" }, IsString),
            (new[] { "samples" }, SamplesRequirement),
            (new[] { "searchablePackages" }, IsArrayOfString),
            (new[] { "type" }, IsString),
            (new[] { "unity" }, IsString),
            (new[] { "unityRelease" }, IsString),
            (new[] { "upm", "changelog" }, IsString),
            (new[] { "upmCi", "footprint" }, IsString),
            (new[] { "version" }, IsString),
        };

        public ManifestTypeVerifierV2(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            var manifest = context.ManifestPermitInvalidJson;

            try
            {
                ValidateAllowedProperties(manifest.Members, location: new List<string>(), context);
            }
            catch (SimpleJsonException e)
            {
                context.AddError("PVP-107-2", e.LegacyMessage);
            }
        }

        static void ValidateAllowedProperties(IEnumerable<Json> members, List<string> location, Verifier.Context context)
        {
            var scratch = new StringBuilder();
            foreach (var member in members)
            {
                location.Add(member.Key);

                // Determine if property fully or partially matches allowed property, or none at all.
                Requirement? fullMatch = null;
                var partialMatch = false;
                foreach (var (allowedPath, requirement) in k_AllowedProperties)
                {
                    // Find length of common location prefix.
                    var length = 0;
                    while (length < location.Count && length < allowedPath.Length && location[length] == allowedPath[length])
                    {
                        length++;
                    }

                    if (length == location.Count)
                    {
                        partialMatch = true;

                        if (length == allowedPath.Length)
                        {
                            fullMatch = requirement;
                            break;
                        }
                    }
                }

                if (fullMatch.HasValue)
                {
                    if (fullMatch.Value.TryGetError(member, scratch, out var error))
                    {
                        context.AddError("PVP-107-2", error);
                    }
                }
                else if (partialMatch)
                {
                    if (member.IsObject)
                    {
                        ValidateAllowedProperties(member.Members, location, context);
                    }
                    else
                    {
                        context.AddError("PVP-107-2", $"{member.Path}: property must be an object");
                    }
                }
                else
                {
                    context.AddError("PVP-107-2", $"{member.Path}: property not allowed");
                }

                location.RemoveAt(location.Count - 1);
            }
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            throw new InvalidOperationException();
        }

        public void Finish()
        {
        }
    }
}
