using System.Text.RegularExpressions;

namespace PvpXray
{
    class ThirdPartyNoticesVerifier : Verifier.IChecker
    {
        const string k_ThirdPartyNotices = "Third-Party Notices.md";
        const string k_Key = "Component Name";
        const string k_Value = "License Type";
        static readonly Regex k_KeyOrValuePattern = new Regex($"^(?:(?<key>{k_Key})|{k_Value}):", RegexOptions.Multiline);

        public static string[] Checks => new[] { "PVP-32-1" }; // Third-Party Notices.md file (US-0065)
        public static int PassCount => 1;

        readonly Verifier.IContext m_Context;

        public ThirdPartyNoticesVerifier(Verifier.IContext context)
        {
            m_Context = context;
        }

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

        public void CheckItem(Verifier.PackageFile file, int passIndex)
        {
            if (file.Path != k_ThirdPartyNotices) return;

            var thirdPartyNotices = file.ReadToString();

            var keyExpected = true; // Value expected if false.
            var startIndex = 0;
            var match = k_KeyOrValuePattern.Match(thirdPartyNotices);

            void AddErrorWithLocation(string checkId, string error) =>
                m_Context.AddError(checkId, $"{k_ThirdPartyNotices}: line {LineNumber(thirdPartyNotices, match.Index)}: {error}");

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
                m_Context.AddError("PVP-32-1", $"{k_ThirdPartyNotices}: third-party notices file must have at least one \"{k_Key}\"-\"{k_Value}\" pair if it exists");
            }
            else if (!keyExpected)
            {
                AddErrorWithLocation("PVP-32-1", $"\"{k_Key}\" entry must be followed by \"{k_Value}\" entry");
            }
        }

        public void Finish()
        {
        }
    }
}
