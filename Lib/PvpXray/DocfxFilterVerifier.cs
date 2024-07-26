using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PvpXray
{
    class DocfxFilterVerifier : Verifier.IChecker
    {
        const string k_Filter = "Documentation~/filter.yml";
        static readonly HashSet<string> k_SupportedTypes = new HashSet<string>(new[]
        {
            "Class", "Delegate", "Enum", "Event", "Field", "Interface",
            "Member", "Method", "Namespace", "Property", "Struct", "Type",
        });
        static readonly Regex k_UnescapeUnquantifiedPeriod = new Regex(@"(?<![\\.(])\.(?![.)*+?{])");

        public static string[] Checks { get; } = { "PVP-155-1", "PVP-156-1", "PVP-157-1" };
        public static int PassCount => 1;

        readonly Verifier.Context m_Context;
        bool m_DiscardNonStandardRuleErrors;
        List<string> m_NonStandardRuleErrors;

        public DocfxFilterVerifier(Verifier.Context context)
        {
            context.IsLegacyCheckerEmittingLegacyJsonErrors = true;
            m_Context = context;
        }

        void Verify(Yaml filter)
        {
            if (!CheckMapping(filter, "apiRules")) return;

            var apiRules = filter["apiRules"];
            if (!CheckSequence(apiRules)) return;

            foreach (var rule in apiRules.Elements)
            {
                EmitAndResetNonStandardRuleErrors();

                if (!CheckMapping(rule, "exclude", "include")) continue;

                // Tweak error message for include rule.
                var include = rule["include"];
                if (include.IsPresent)
                {
                    AddError("PVP-155-1", $"{include.Path}: include rule not allowed");
                }

                var exclude = rule["exclude"];
                if (!CheckMapping(exclude, "type", "uidRegex", "hasAttribute")) continue;

                var type = exclude["type"];
                if (type.IsPresent)
                {
                    if (CheckString(type))
                    {
                        CheckSupportedType(type);
                        AddError("PVP-157-1", $"non-standard exclude type: {type.String}");
                    }
                }

                var uidRegex = exclude["uidRegex"];
                if (uidRegex.IsPresent)
                {
                    if (CheckString(uidRegex))
                    {
                        try
                        {
                            _ = new Regex(uidRegex.String);
                            CheckSensibleUidRegex(uidRegex);
                        }
                        catch (ArgumentException)
                        {
                            AddError("PVP-155-1", $"{uidRegex.Path}: invalid regex pattern: {uidRegex.String}");
                        }

                        AddError("PVP-157-1", $"non-standard exclude uidRegex: {uidRegex.String}");
                    }
                }

                var hasAttribute = exclude["hasAttribute"];
                if (hasAttribute.IsPresent && CheckMapping(hasAttribute, "uid", "ctorArguments"))
                {
                    string uidString = null;
                    List<string> ctorArgumentsStrings = null;

                    var uid = hasAttribute["uid"];
                    if (uid.IsPresent)
                    {
                        if (CheckString(uid)) uidString = uid.String;
                    }
                    else
                    {
                        AddError("PVP-155-1", $"{hasAttribute.Path}: missing required key \"uid\"");
                    }

                    var ctorArguments = hasAttribute["ctorArguments"];
                    if (ctorArguments.IsPresent && CheckSequence(ctorArguments))
                    {
                        ctorArgumentsStrings = new List<string>();
                        foreach (var argument in ctorArguments.Elements)
                        {
                            if (CheckString(argument))
                            {
                                ctorArgumentsStrings?.Add(argument.String);
                            }
                            else
                            {
                                ctorArgumentsStrings = null;
                            }
                        }
                    }

                    if (uidString != null // (uid is present and is a string)
                        && (uidString != "System.ComponentModel.EditorBrowsableAttribute"
                            || ctorArgumentsStrings == null // (ctorArguments is not present or is not a sequence of strings)
                            || ctorArgumentsStrings.Count != 1
                            || ctorArgumentsStrings[0] != "System.ComponentModel.EditorBrowsableState.Never"))
                    {
                        var detail = ctorArgumentsStrings == null
                            ? uidString
                            : $"{uidString}({string.Join(", ", ctorArgumentsStrings)})";
                        AddError("PVP-157-1", $"non-standard exclude hasAttribute: {detail}");
                    }
                }

                if (!uidRegex.IsPresent && !hasAttribute.IsPresent)
                {
                    AddError("PVP-155-1", $"{exclude.Path}: missing required key \"uidRegex\" or \"hasAttribute\"");
                }
            }

            EmitAndResetNonStandardRuleErrors();
        }

        bool CheckMapping(Yaml node, params string[] allowedKeys)
        {
            if (!node.IsMapping)
            {
                AddError("PVP-155-1", $"{node.Path}: must be a mapping");
                return false;
            }

            foreach (var member in node.Members)
            {
                if (!allowedKeys.Contains(member.Key))
                {
                    AddError("PVP-155-1", $"{member.Path}: unknown key");
                }
            }

            return true;
        }

        bool CheckSequence(Yaml node)
        {
            if (node.IsSequence) return true;
            AddError("PVP-155-1", $"{node.Path}: must be a sequence");
            return false;
        }

        bool CheckString(Yaml node)
        {
            if (node.IsString) return true;
            AddError("PVP-155-1", $"{node.Path}: must be a string");
            return false;
        }

        void CheckSupportedType(Yaml type)
        {
            var typeString = type.String;
            var typeStringLower = typeString.ToLowerInvariant();
            foreach (var supportedType in k_SupportedTypes)
            {
                if (typeString == supportedType) return;
                if (typeStringLower == supportedType.ToLowerInvariant())
                {
                    AddError("PVP-155-1", $"{type.Path}: wrong capitalization of \"{supportedType}\"");
                    return;
                }
            }

            AddError("PVP-155-1", $"{type.Path}: unsupported type: {type.String}");
        }

        void CheckSensibleUidRegex(Yaml uidRegex)
        {
            var pattern = uidRegex.String;
            if (!(pattern.StartsWithOrdinal("^") || pattern.StartsWithOrdinal(".*") || pattern.StartsWithOrdinal(@"\."))
                || !(pattern.EndsWithOrdinal("$") || pattern.EndsWithOrdinal(".*") || pattern.EndsWithOrdinal(@"\.")))
            {
                AddError("PVP-156-1", $"regex should be anchored: {pattern}");
            }

            if (k_UnescapeUnquantifiedPeriod.IsMatch(pattern))
            {
                AddError("PVP-156-1", $"unescaped period: {pattern}");
            }
        }

        void AddError(string checkId, string error)
        {
            error = $"{k_Filter}: {error}";

            // PVP-155-1 and PVP-157-1 are mutually exclusive within each rule.
            switch (checkId)
            {
                case "PVP-155-1" when !m_DiscardNonStandardRuleErrors:
                    m_DiscardNonStandardRuleErrors = true;
                    m_NonStandardRuleErrors?.Clear();
                    break;

                case "PVP-157-1":
                    if (!m_DiscardNonStandardRuleErrors)
                    {
                        m_NonStandardRuleErrors = m_NonStandardRuleErrors ?? new List<string>();
                        m_NonStandardRuleErrors.Add(error);
                    }
                    return; // Don't emit error.
            }

            m_Context.AddError(checkId, error);
        }

        void EmitAndResetNonStandardRuleErrors()
        {
            foreach (var error in m_NonStandardRuleErrors ?? Enumerable.Empty<string>())
            {
                m_Context.AddError("PVP-157-1", error);
            }

            m_DiscardNonStandardRuleErrors = false;
            m_NonStandardRuleErrors?.Clear();
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Path != k_Filter) return;

            Verify(file.ReadAsYamlLax());
        }

        public void Finish()
        {
        }
    }
}
