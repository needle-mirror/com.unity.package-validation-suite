using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using UnityEngine;

#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#endif

namespace UnityEditor.PackageManager.ValidationSuite.UI
{
#if UNITY_2019_1_OR_NEWER
    internal class ValidationSuiteExtensionUI : VisualElement
    {
        private const string PackagePath = "Packages/com.unity.package-validation-suite/";
        private const string ResourcesPath = PackagePath + "Editor/Resources/";
        private const string TemplatePath = ResourcesPath + "Templates/ValidationSuiteTools.uxml";
        private const string DarkStylePath = ResourcesPath + "Styles/Dark.uss";
        private const string LightStylePath = ResourcesPath + "Styles/Light.uss";

        private VisualElement root;

        private PopupField<ValidationType> validationPopupField;

        static readonly List<ValidationType> k_ValidationChoices = new List<ValidationType> { ValidationType.CI, ValidationType.Promotion };
        const ValidationType k_DefaultValidationType = ValidationType.Promotion;

        private PackageInfo CurrentPackageinfo { get; set; }
        PackageId PackageId { get; set; }

        public static ValidationSuiteExtensionUI CreateUI()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            return asset == null ? null : new ValidationSuiteExtensionUI(asset);
        }

        private ValidationSuiteExtensionUI(VisualTreeAsset asset)
        {
            root = asset.CloneTree();
            string path = EditorGUIUtility.isProSkin ? DarkStylePath : LightStylePath;
            var styleSheet = EditorGUIUtility.Load(path) as StyleSheet;
            root.styleSheets.Add(styleSheet);
            Add(root);

            validationPopupField = new PopupField<ValidationType>(k_ValidationChoices, k_DefaultValidationType, FormatValidationType, FormatValidationType);
            root.Q<VisualElement>("ValidationTypeDropdown").Add(validationPopupField);

            ValidateButton.clickable.clicked += Validate;
            ViewResultsButton.clickable.clicked += ViewResults;
            ViewDiffButton.clickable.clicked += ViewDiffs;
        }

        static string FormatValidationType(ValidationType validationType)
        {
            switch (validationType)
            {
                case ValidationType.CI: return "Against Unity candidates standards";
                case ValidationType.Promotion: return "Against Unity production standards";
                default: throw new InvalidEnumArgumentException(nameof(validationType));
            }
        }

        static bool PackageAvailable(PackageInfo packageInfo)
        {
#if UNITY_2019_2_OR_NEWER
            var installedPackageInfo = PackageInfo.FindForAssetPath($"Packages/{packageInfo.name}");
            return installedPackageInfo != null && installedPackageInfo.version == packageInfo.version;
#else
            return packageInfo.status == PackageStatus.Available || packageInfo.status == PackageStatus.Error;
#endif
        }

        public static bool SourceSupported(PackageInfo info)
        {
            PackageSource source = info.source;
#if UNITY_2019_3_OR_NEWER
            // Tarball is available here only, so check if its a tarball and return true
            if (source == PackageSource.LocalTarball) return true;
#endif

            return source == PackageSource.Embedded
                || source == PackageSource.Local
                || source == PackageSource.Registry
                || source == PackageSource.Git
                || (info.source == PackageSource.BuiltIn && info.type != "module");
        }

        public void OnPackageSelectionChange(PackageInfo packageInfo)
        {
            if (root == null)
                return;

            var showValidationUI = packageInfo != null && PackageAvailable(packageInfo) && SourceSupported(packageInfo);
            UIUtils.SetElementDisplay(this, showValidationUI);
            if (!showValidationUI)
                return;

            CurrentPackageinfo = packageInfo;
            PackageId = new PackageId(CurrentPackageinfo);
            ValidationResults.text = string.Empty;

            validationPopupField.value = k_DefaultValidationType;

            UIUtils.SetElementDisplay(ViewResultsButton, ValidationSuiteReport.ReportExists(PackageId.Id));
            UIUtils.SetElementDisplay(ViewDiffButton, ValidationSuiteReport.DiffsReportExists(PackageId.Id));

            root.style.backgroundColor = Color.gray;
        }

        private void Validate()
        {
            if (root == null)
                return;

            if (Utilities.NetworkNotReachable)
            {
                EditorUtility.DisplayDialog("", "Validation suite requires network access and cannot be used offline.", "Ok");
                return;
            }

            var validationType = validationPopupField.value;

            var results = ValidationSuite.ValidatePackage(PackageId, validationType);
            var report = ValidationSuiteReport.GetReport(PackageId.Id);

            UIUtils.SetElementDisplay(ViewResultsButton, ValidationSuiteReport.ReportExists(PackageId.Id));
            UIUtils.SetElementDisplay(ViewDiffButton, ValidationSuiteReport.DiffsReportExists(PackageId.Id));

            if (!results)
            {
                ValidationResults.text = "Failed";
                root.style.backgroundColor = Color.red;
            }
            else if (report != null && report.Tests.Any(t => t.TestOutput.Any(o => o.Type == TestOutputType.Warning)))
            {
                ValidationResults.text = "Warnings";
                root.style.backgroundColor = Color.yellow;
            }
            else
            {
                ValidationResults.text = "Success";
                root.style.backgroundColor = Color.green;
            }
        }

        private void ViewResults()
        {
            var filePath = TextReport.ReportPath(PackageId.Id);
            try
            {
                try
                {
                    var targetFile = Directory.GetCurrentDirectory() + "/" + filePath;
                    if (!File.Exists(targetFile))
                        throw new Exception("Validation Result not found!");

                    Process.Start(targetFile);
                }
                catch (Exception)
                {
                    var data = File.ReadAllText(filePath);
                    EditorUtility.DisplayDialog("Validation Results", data, "Ok");
                }
            }
            catch (Exception)
            {
                EditorUtility.DisplayDialog("Validation Results", "Results are missing", "Ok");
            }
        }

        private void ViewDiffs()
        {
            if (ValidationSuiteReport.DiffsReportExists(PackageId.Id))
            {
                Application.OpenURL("file://" + Path.GetFullPath(ValidationSuiteReport.DiffsReportPath(PackageId.Id)));
            }
        }

        internal Label ValidationResults { get { return root.Q<Label>("validationResults");} }

        internal Button ValidateButton { get { return root.Q<Button>("validateButton"); } }

        internal Button ViewResultsButton { get { return root.Q<Button>("viewResults"); } }

        internal Button ViewDiffButton { get { return root.Q<Button>("viewdiff"); } }
    }
#endif
}
