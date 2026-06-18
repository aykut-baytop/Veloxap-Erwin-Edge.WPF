using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeloxapEDGEWpfLib.Models;
using VeloxapEDGEWpfLib.Services;

namespace VeloxapEDGEWpfLib.Pages
{
    public partial class ModelValidationView : UserControl
    {
        private readonly ModelInfo modelInfo;
        private readonly List<string> validationRules;
        private readonly RuleService ruleService;
        private readonly string catalogName;
        private readonly string catalogLongId;
        private string currentAlterDdl;
        private bool isInitializing;
        private bool isUpdatingTargetVersion;
        private bool isValidationOk;
        private bool isSendingApproval;

        public ModelValidationView()
            : this(null, null, null, null, null)
        {
        }

        internal ModelValidationView(ModelInfo modelInfo, IEnumerable<string> validationRules)
            : this(modelInfo, validationRules, null, null, null)
        {
        }

        internal ModelValidationView(ModelInfo modelInfo, IEnumerable<string> validationRules, RuleService ruleService)
            : this(modelInfo, validationRules, ruleService, null, null)
        {
        }

        internal ModelValidationView(
            ModelInfo modelInfo,
            IEnumerable<string> validationRules,
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            InitializeComponent();

            this.modelInfo = modelInfo;
            this.validationRules = validationRules?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                ?? new List<string>();
            this.ruleService = ruleService;
            this.catalogName = catalogName;
            this.catalogLongId = catalogLongId;

            LoadVersionSelectors();
            ResetValidationState("Validasyon bekleniyor.");
        }

        private void LoadVersionSelectors()
        {
            isInitializing = true;

            var versions = LoadAvailableVersions(modelInfo);
            cmbSourceVersion.ItemsSource = versions;
            cmbTargetVersion.ItemsSource = versions.ToList();

            if (versions.Count > 0)
            {
                cmbSourceVersion.SelectedIndex = 0;
                SelectPreviousTargetVersion();
            }

            isInitializing = false;
            _ = RefreshAlterDdlPreviewAsync();
        }

        private async void VersionSelection_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || isUpdatingTargetVersion)
                return;

            if (sender == cmbSourceVersion)
                SelectPreviousTargetVersion();

            ResetValidationState("Versiyon seçimi değişti. Validasyon tekrar çalıştırılmalı.");
            await RefreshAlterDdlPreviewAsync();
        }

        private void BtnRunValidation_Click(object sender, RoutedEventArgs e)
        {
            RunValidation();
        }

        private async void BtnSendApproval_Click(object sender, RoutedEventArgs e)
        {
            if (!isValidationOk)
                return;

            await SendApprovalAsync();
        }

        private async Task SendApprovalAsync()
        {
            if (isSendingApproval)
                return;

            if (ruleService == null)
            {
                SetStatus("Approval servisi kullanilabilir degil.");
                return;
            }

            var sourceVersion = cmbSourceVersion.SelectedItem as VersionOption;
            if (sourceVersion == null)
            {
                SetStatus("Onaya gondermek icin kaynak versiyon secilmeli.");
                return;
            }

            if (!int.TryParse(sourceVersion.VersionNo, out int versionId))
            {
                SetStatus("Kaynak versiyon numarasi okunamadi.");
                return;
            }

            int targetVersionId = versionId - 1;
            if (targetVersionId < 1)
            {
                SetStatus("Onaya gondermek icin onceki hedef versiyon bulunamadi.");
                return;
            }

            string cName = ResolveCatalogName();
            string cLongId = ResolveCatalogLongId(sourceVersion);

            if (string.IsNullOrWhiteSpace(cName) || string.IsNullOrWhiteSpace(cLongId))
            {
                SetStatus("Onaya gondermek icin cName veya cLongId okunamadi.");
                return;
            }

            string description = PromptForValidationDescription();
            if (description == null)
            {
                SetStatus("Onaya gonderme iptal edildi.");
                return;
            }

            try
            {
                isSendingApproval = true;
                btnSendApproval.IsEnabled = false;
                SetStatus("Onaya gonderiliyor...");

                string response = await ruleService.StartApprovalByCatalogAsync(
                    RuleApiSettings.GetApprovalStartByCatalogUrl(),
                    cName,
                    cLongId,
                    versionId,
                    targetVersionId,
                    description,
                    currentAlterDdl ?? string.Empty);

                SetStatus(
                    "Onaya gonderildi. cName: " + cName +
                    ", cLongId: " + cLongId +
                    ", versionId: " + versionId +
                    ", targetVersionId: " + targetVersionId +
                    ", responseLength: " + (response == null ? 0 : response.Length) + ".");
            }
            catch (Exception ex)
            {
                SetStatus("Onaya gonderme istegi sirasinda hata olustu.");
                txtValidationResults.Text = ex.ToString();
            }
            finally
            {
                isSendingApproval = false;
                btnSendApproval.IsEnabled = isValidationOk;
            }
        }

        private string PromptForValidationDescription()
        {
            var owner = Window.GetWindow(this);
            var dialog = new Window
            {
                Title = "Validasyon Aciklamasi",
                Width = 460,
                Height = 290,
                MinWidth = 380,
                MinHeight = 250,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var root = new Grid
            {
                Margin = new Thickness(16)
            };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Onaya gonderilecek validasyon aciklamasini girin.",
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };

            var descriptionBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 100
            };

            var validationMessage = new TextBlock
            {
                Text = "Lutfen daha uzun bir aciklama girin.",
                Foreground = System.Windows.Media.Brushes.Firebrick,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var cancelButton = new Button
            {
                Content = "Iptal",
                Width = 86,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };

            var okButton = new Button
            {
                Content = "Tamam",
                Width = 86,
                Height = 30,
                IsDefault = true,
                IsEnabled = false
            };

            descriptionBox.TextChanged += (sender, args) =>
            {
                okButton.IsEnabled = !string.IsNullOrWhiteSpace(descriptionBox.Text);
                validationMessage.Visibility = Visibility.Collapsed;
            };

            okButton.Click += (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(descriptionBox.Text) || descriptionBox.Text.Trim().Length <= 5)
                {
                    validationMessage.Visibility = Visibility.Visible;
                    descriptionBox.Focus();
                    descriptionBox.SelectAll();
                    return;
                }

                dialog.DialogResult = true;
            };

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(okButton);

            Grid.SetRow(label, 0);
            Grid.SetRow(descriptionBox, 1);
            Grid.SetRow(validationMessage, 2);
            Grid.SetRow(buttons, 3);

            root.Children.Add(label);
            root.Children.Add(descriptionBox);
            root.Children.Add(validationMessage);
            root.Children.Add(buttons);

            dialog.Content = root;
            dialog.Loaded += (sender, args) => Keyboard.Focus(descriptionBox);

            return dialog.ShowDialog() == true
                ? descriptionBox.Text.Trim()
                : null;
        }

        private void RunValidation()
        {
            if (modelInfo == null)
            {
                ResetValidationState("Seçili model bulunamadı.");
                txtValidationResults.Text = "Validasyon çalıştırmak için önce bir model seçilmeli.";
                return;
            }

            var rules = GetValidationRules().ToList();
            if (rules.Count == 0)
            {
                ResetValidationState("Validasyon kuralı bulunamadı.");
                txtValidationResults.Text =
                    "Kural listesi boş. ValidationRulesView veya kalıcı kural kaynağı bağlandığında bu test çalışacak.";
                return;
            }

            try
            {
                SetStatus("Validasyon çalışıyor...");
                var issues = CrossRuleValidationEngine.Validate(modelInfo, rules, runParallel: true);

                isValidationOk = issues.Count == 0;
                btnSendApproval.IsEnabled = isValidationOk;

                txtValidationResults.Text = FormatValidationResults(issues);
                SetStatus(FormatValidationSummary(issues));
            }
            catch (Exception ex)
            {
                ResetValidationState("Validasyon sırasında hata oluştu.");
                txtValidationResults.Text = ex.ToString();
            }
        }

        private IEnumerable<string> GetValidationRules()
        {
            // TODO: ValidationRulesView veya kalıcı kural kaynağı hazır olduğunda kuralları buradan döndür.
            return validationRules;
        }

        private static string FormatValidationResults(IReadOnlyCollection<CrossValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return "Validasyon hatası bulunamadı.";

            var builder = new StringBuilder();
            int index = 1;

            foreach (var issue in issues)
            {
                builder.AppendLine($"{index}. {issue.Message}");
                builder.AppendLine($"   Kural: {issue.RuleText}");
                builder.AppendLine($"   Nesne: {issue.CheckObjectPath}");
                builder.AppendLine($"   Property: {issue.PropertyName}");
                builder.AppendLine($"   Beklenen: {issue.ExpectedValue}");
                builder.AppendLine($"   Gerçek: {issue.ActualValue}");
                builder.AppendLine();
                index++;
            }

            return builder.ToString();
        }

        private static string FormatValidationSummary(IReadOnlyCollection<CrossValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return "Validasyon başarılı. Onaya gönderilebilir.";

            var builder = new StringBuilder();
            builder.AppendLine($"Validasyon tamamlandı. {issues.Count} hata bulundu.");

            foreach (var issue in issues.Take(3))
                builder.AppendLine("- " + (issue.CheckObjectPath ?? issue.Message));

            if (issues.Count > 3)
                builder.AppendLine("Detaylar Validasyon Sonuçları sekmesinde.");

            return builder.ToString();
        }

        private async Task RefreshAlterDdlPreviewAsync()
        {
            var sourceVersion = cmbSourceVersion.SelectedItem as VersionOption;
            var targetVersion = cmbTargetVersion.SelectedItem as VersionOption;

            if (sourceVersion == null || targetVersion == null)
            {
                currentAlterDdl = string.Empty;
                txtAlterDdl.Text = "Kaynak ve hedef versiyon seçimi bekleniyor.";
                return;
            }

            try
            {
                currentAlterDdl = string.Empty;
                txtAlterDdl.Text = "Alter DDL hazırlanıyor...";
                string ddl = await RequestAlterDdlFromApiAsync(sourceVersion, targetVersion);

                currentAlterDdl = string.IsNullOrWhiteSpace(ddl)
                    ? string.Empty
                    : ddl;

                txtAlterDdl.Text = string.IsNullOrWhiteSpace(ddl)
                    ? BuildAlterDdlPlaceholder(sourceVersion, targetVersion)
                    : ddl;
            }
            catch (Exception ex)
            {
                currentAlterDdl = string.Empty;
                txtAlterDdl.Text = ex.ToString();
                SetStatus("Alter DDL isteği sırasında hata oluştu.");
            }
        }

        private async Task<string> RequestAlterDdlFromApiAsync(VersionOption sourceVersion, VersionOption targetVersion)
        {
            if (ruleService == null)
                return string.Empty;

            if (!int.TryParse(sourceVersion.VersionNo, out int sourceVNo))
                throw new InvalidOperationException("Kaynak versiyon numarasi okunamadi.");

            int targetVNo = sourceVNo - 1;
            if (targetVNo < 1)
                throw new InvalidOperationException("Alter DDL icin onceki versiyon bulunamadi.");

            string path = ExtractAlterDdlPath(sourceVersion.Locator);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Alter DDL path degeri secili modelden okunamadi.");

            string ddl = await ruleService.GetAlterDdlAsync(
                RuleApiSettings.GetAlterDdlUrl(),
                path,
                sourceVNo,
                targetVNo);

            SetStatus("Alter DDL hazirlandi. Kaynak: " + sourceVNo + ", Hedef: " + targetVNo + ".");
            return FormatDdlForDisplay(ddl);
        }

        private void SelectPreviousTargetVersion()
        {
            var sourceVersion = cmbSourceVersion.SelectedItem as VersionOption;
            if (sourceVersion == null || !int.TryParse(sourceVersion.VersionNo, out int sourceVNo))
                return;

            string targetVersionNo = (sourceVNo - 1).ToString();
            var targetVersion = cmbTargetVersion.Items
                .OfType<VersionOption>()
                .FirstOrDefault(option => string.Equals(
                    option.VersionNo,
                    targetVersionNo,
                    StringComparison.OrdinalIgnoreCase));

            if (targetVersion == null)
                return;

            try
            {
                isUpdatingTargetVersion = true;
                cmbTargetVersion.SelectedItem = targetVersion;
            }
            finally
            {
                isUpdatingTargetVersion = false;
            }
        }

        private static string ExtractAlterDdlPath(string locator)
        {
            if (string.IsNullOrWhiteSpace(locator))
                return string.Empty;

            string value = locator.Trim();
            int start = value.IndexOf("Mart//", StringComparison.OrdinalIgnoreCase);

            if (start < 0)
                start = value.IndexOf("Mart/", StringComparison.OrdinalIgnoreCase);

            if (start < 0)
                start = 0;

            int end = value.IndexOf('?', start);
            if (end < 0)
                end = value.Length;

            if (end <= start)
                return string.Empty;

            string path = value.Substring(start, end - start).Trim(' ', '(', ')');
            while (path.Contains("//"))
                path = path.Replace("//", "/");

            return Uri.UnescapeDataString(path);
        }

        private static string FormatDdlForDisplay(string ddl)
        {
            if (string.IsNullOrEmpty(ddl))
                return string.Empty;

            var builder = new StringBuilder(ddl.Length);

            for (int i = 0; i < ddl.Length; i++)
            {
                char current = ddl[i];

                if (current == '\r')
                {
                    if (i + 1 < ddl.Length && ddl[i + 1] == '\n')
                        i++;

                    builder.Append(Environment.NewLine);
                    continue;
                }

                if (current == '\n')
                {
                    builder.Append(Environment.NewLine);
                    continue;
                }

                if (current != '\\' || i == ddl.Length - 1)
                {
                    builder.Append(current);
                    continue;
                }

                if (i + 3 < ddl.Length && ddl[i + 1] == 'r' && ddl[i + 2] == '\\' && ddl[i + 3] == 'n')
                {
                    builder.Append(Environment.NewLine);
                    i += 3;
                    continue;
                }

                char next = ddl[i + 1];
                switch (next)
                {
                    case 'n':
                        builder.Append(Environment.NewLine);
                        i++;
                        break;
                    case 'r':
                        builder.Append(Environment.NewLine);
                        i++;
                        break;
                    case 't':
                        builder.Append('\t');
                        i++;
                        break;
                    case '\\':
                        builder.Append('\\');
                        i++;
                        break;
                    default:
                        builder.Append(current);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string BuildAlterDdlPlaceholder(VersionOption sourceVersion, VersionOption targetVersion)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Alter DDL cevabi bos dondu.");
            builder.AppendLine();
            builder.AppendLine($"Kaynak: {sourceVersion.DisplayName}");
            builder.AppendLine($"Hedef: {targetVersion.DisplayName}");
            builder.AppendLine();
            builder.AppendLine("API ddl alani dolu dondugunde sonuc burada gosterilecek.");

            return builder.ToString();
        }

        private void ResetValidationState(string message)
        {
            isValidationOk = false;
            btnSendApproval.IsEnabled = false;
            SetStatus(message);
        }

        private void SetStatus(string message)
        {
            txtStatusMessage.Text = message ?? string.Empty;
        }

        private string ResolveCatalogName()
        {
            if (!string.IsNullOrWhiteSpace(catalogName))
                return catalogName.Trim();

            string modelName = modelInfo?.getoName();
            return string.IsNullOrWhiteSpace(modelName) ? string.Empty : modelName.Trim();
        }

        private string ResolveCatalogLongId(VersionOption sourceVersion)
        {
            if (!string.IsNullOrWhiteSpace(catalogLongId))
                return catalogLongId.Trim();

            if (!string.IsNullOrWhiteSpace(sourceVersion?.ModelLongId))
                return sourceVersion.ModelLongId.Trim();

            string locator = modelInfo?.getoLocation();
            string modelLongId = ExtractQueryValue(locator, "modelLongId");
            return string.IsNullOrWhiteSpace(modelLongId) ? string.Empty : modelLongId.Trim();
        }

        private static List<VersionOption> LoadAvailableVersions(ModelInfo modelInfo)
        {
            var currentVersion = BuildCurrentVersionOption(modelInfo);

            if (!int.TryParse(currentVersion.VersionNo, out int latestVersion) || latestVersion < 1)
                return new List<VersionOption> { currentVersion };

            var versions = new List<VersionOption>();
            for (int version = latestVersion; version >= 1; version--)
            {
                versions.Add(new VersionOption(
                    BuildVersionDisplayName(modelInfo, version),
                    version.ToString(),
                    currentVersion.ModelLongId,
                    currentVersion.Locator));
            }

            return versions;
        }

        private static VersionOption BuildCurrentVersionOption(ModelInfo modelInfo)
        {
            string modelName = modelInfo?.getoName();
            string locator = modelInfo?.getoLocation();
            string versionNo = ExtractQueryValue(locator, "version");
            string modelLongId = ExtractQueryValue(locator, "modelLongId");

            string displayName = string.IsNullOrWhiteSpace(versionNo)
                ? "Geçerli Versiyon"
                : "Versiyon " + versionNo;

            if (!string.IsNullOrWhiteSpace(modelName))
                displayName += " - " + modelName;

            return new VersionOption(displayName, versionNo, modelLongId, locator);
        }

        private static string BuildVersionDisplayName(ModelInfo modelInfo, int version)
        {
            string modelName = modelInfo?.getoName();
            string displayName = "Versiyon " + version;

            if (!string.IsNullOrWhiteSpace(modelName))
                displayName += " - " + modelName;

            return displayName;
        }

        private static string ExtractQueryValue(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var match = Regex.Match(
                value,
                @"(?:\?|&)" + Regex.Escape(key) + @"=([^&\s\)]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : string.Empty;
        }

        private sealed class VersionOption
        {
            public VersionOption(string displayName, string versionNo, string modelLongId, string locator)
            {
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Versiyon" : displayName;
                VersionNo = versionNo ?? string.Empty;
                ModelLongId = modelLongId ?? string.Empty;
                Locator = locator ?? string.Empty;
            }

            public string DisplayName { get; }

            public string VersionNo { get; }

            public string ModelLongId { get; }

            public string Locator { get; }
        }
    }
}
