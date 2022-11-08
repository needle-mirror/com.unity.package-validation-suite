using System.Linq;
using System.Text.RegularExpressions;

namespace PureFileValidationPvp
{
    static class ThirdPartyNoticesValidations
    {
        const string k_ThirdPartyNotices = "Third-Party Notices.md";
        const string k_Key = "Component Name";
        const string k_Value = "License Type";
        static readonly Regex k_KeyOrValuePattern = new Regex($"^(?:(?<key>{k_Key})|{k_Value}):", RegexOptions.Multiline);

        public static readonly string[] Checks = { "PVP-32-1" }; // Third-Party Notices.md file (US-0065)

        static int LineNumber(string text, int index)
        {
            var lineNumber = 1;
            for (var i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    lineNumber++;
                }
            }

            return lineNumber;
        }

        public static void Run(Validator.Context context)
        {
            if (!context.Files.Contains(k_ThirdPartyNotices))
            {
                return;
            }

            var thirdPartyNotices = context.ReadFileToString(k_ThirdPartyNotices);

            var keyExpected = true; // Value expected if false.
            var startIndex = 0;
            var match = k_KeyOrValuePattern.Match(thirdPartyNotices);

            void AddErrorWithLocation(string checkId, string error) =>
                context.AddError(checkId, $"{k_ThirdPartyNotices}: line {LineNumber(thirdPartyNotices, match.Index)}: {error}");

            // Check that key and value entries come in ordered pairs
            while (match.Success)
            {
                var keyFound = match.Groups["key"].Success;
                if (keyFound != keyExpected)
                {
                    AddErrorWithLocation("PVP-32-1", keyExpected
                        ? $"expected \"{k_Key}\" entry but found \"{k_Value}\" entry"
                        : $"expected \"{k_Value}\" entry but found \"{k_Key}\" entry");

                    return; // Nothing left to check.
                }

                keyExpected = !keyExpected;
                startIndex = match.Index + match.Length;
                match = k_KeyOrValuePattern.Match(thirdPartyNotices, startIndex);
            }

            if (startIndex == 0)
            {
                context.AddError("PVP-32-1", $"{k_ThirdPartyNotices}: third-party notices file must have at least one \"{k_Key}\"-\"{k_Value}\" pair if it exists");
            }
            else if (!keyExpected)
            {
                AddErrorWithLocation("PVP-32-1", $"\"{k_Key}\" entry must be followed by \"{k_Value}\" entry");
            }
        }
    }
}
