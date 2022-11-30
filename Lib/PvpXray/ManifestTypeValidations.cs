using System.Collections.Generic;
using System.Linq;

using Requirement = PvpXray.ManifestValidations.Requirement;

namespace PvpXray
{
    static class ManifestTypeValidations
    {
        public static readonly string[] Checks = { "PVP-107-2" };

        static Requirement IsBoolean => new Requirement() { Message = "must be a boolean", Func = json => json.IsBoolean };
        static Requirement IsNumber => new Requirement() { Message = "must be a number", Func = json => json.IsNumber };
        static Requirement IsString => new Requirement() { Message = "must be a string", Func = json => json.IsString };
        static Requirement IsArrayOfString => new Requirement()
        {
            Message = "must be an array of string",
            Func = json => json.IsArray && json.Elements.All(element => element.IsString)
        };
        static Requirement IsObjectOfString => new Requirement()
        {
            Message = "must be an object with string values",
            Func = json => json.IsObject && json.Members.All(element => element.IsString)
        };
        static Requirement SamplesRequirement => new Requirement()
        {
            Message = "must be an array of objects with allowed string entries \"description\", \"displayName\", and \"path\" and allowed boolean entry \"interactiveImport\"",
            Func = json =>
                json.IsArray && json.Elements.All(element =>
                    element.IsObject && element.Members.All(member =>
                        new[] { "description", "displayName", "interactiveImport", "path" }.Contains(member.Key)
                        && (member.Key == "interactiveImport" ? member.IsBoolean : member.IsString)))
        };

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

        static void ValidateAllowedProperties(IEnumerable<Json> members, List<string> location, Validator.Context context)
        {
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
                    var requirement = fullMatch.Value;
                    if (!requirement.Func(member))
                    {
                        context.AddError("PVP-107-2", $"{member.Path}: {requirement.Message}");
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

        public static void Run(Validator.Context context)
        {
            var manifest = context.Manifest;

            try
            {
                ValidateAllowedProperties(manifest.Members, location: new List<string>(), context);
            }
            catch (JsonException e)
            {
                context.AddError("PVP-107-2", e.Message);
            }
        }
    }
}
