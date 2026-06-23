using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeloxapEDGErwinTools.AddIn;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Pages;
using Veloxap.AddIn.Erwin.Services;

namespace Veloxap.AddIn.Erwin
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        private SCAPI.Application oApp;
        private VeloxapEDGErwinLib veloxapEDGErwinLib;
        private List<string> rules;
        private List<Rule> validationRules;
        private List<(string value, string key1, string key2)> models;
        private AuthTokenProvider authTokenProvider;
        private CookieContainer apiCookieContainer;
        private RuleService ruleService;

        private string selectedModelLongId;
        private string selectedModelName;
        private string selectedModelVersionNo;
        private List<(int key, string val)> selectedModelAllVersions;
        private ModelInfo currentModelInfo;
        private string loadedRulesModelKey;
        private string lastRuleRequestTrace;

        private bool isValidationActive;
        private bool isValidationOk;

        public Window1()
        {
            InitializeComponent();
            rbModelInfo.Checked += Menu_Checked;
            if (RuleApiSettings.IsModelComparisonEnabled())
            {
                rbCompare.Checked += Menu_Checked;
                rbCompare.Visibility = Visibility.Visible;
            }
            else
            {
                rbCompare.Visibility = Visibility.Collapsed;
            }
            rbTableUdps.Checked += Menu_Checked;
            rbValidation.Checked += Menu_Checked;
            //rbRules.Checked += Menu_Checked;
            //rbRules.Visibility = Visibility.Collapsed;
            cmbMainModel.SelectionChanged += CmbMainModel_SelectionChanged;

            MainContent.Content = new ModelInfoView();
        }

        public void Init(ref SCAPI.Application app)
        {
            oApp = app;
            veloxapEDGErwinLib = new VeloxapEDGErwinLib(ref app);
            models = new List<(string value, string key1, string key2)>();
            rules = new List<string>();
            validationRules = new List<Rule>();
            apiCookieContainer = new CookieContainer();
            authTokenProvider = new AuthTokenProvider(CreatePlainHttpClient(apiCookieContainer));
            ruleService = new RuleService(CreateAuthorizedHttpClient(authTokenProvider, apiCookieContainer));
            isValidationActive = true;
            isValidationOk = false;
            if (EnsureAuthCredentialsConfigured(showMessage: true))
                _ = InitializeAuthenticationAsync();
            PopulateModels();
        }

        private async void Menu_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == rbModelInfo)
                MainContent.Content = new ModelInfoView(currentModelInfo);

            else if (sender == rbCompare)
            {
                MainContent.Content = new ModelComparisonView(
                    veloxapEDGErwinLib,
                    oApp,
                    models);
            }

            else if (sender == rbTableUdps)
            {
                MainContent.Content = new ModelUdpView(
                    currentModelInfo,
                    oApp,
                    GetCurrentPersistenceUnit());
            }

            else if (sender == rbValidation)
            {
                if (!EnsureAuthCredentialsConfigured(showMessage: true))
                    return;

                await LoadValidationRulesForSelectedModelAsync(showErrors: true);
                MainContent.Content = new ModelValidationView(
                    currentModelInfo,
                    rules,
                    GetRuleService(),
                    selectedModelName,
                    selectedModelLongId,
                    validationRules,
                    false);
            }

            //else if (sender == rbRules)
            //{
            //    if (!EnsureAuthCredentialsConfigured(showMessage: true))
            //        return;

            //    await LoadValidationRulesForSelectedModelAsync(showErrors: true);
            //    MainContent.Content = new ModelValidationView(
            //        currentModelInfo,
            //        rules,
            //        GetRuleService(),
            //        selectedModelName,
            //        selectedModelLongId,
            //        validationRules,
            //        true);
            //}

        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ClearMenuSelection();
            ShowSettingsView();
        }

        private void ClearMenuSelection()
        {
            rbModelInfo.IsChecked = false;
            rbCompare.IsChecked = false;
            rbTableUdps.IsChecked = false;
            rbValidation.IsChecked = false;
            //rbRules.IsChecked = false;
        }

        private void ShowSettingsView()
        {
            MainContent.Content = new SettingsView();
        }

        private bool EnsureAuthCredentialsConfigured(bool showMessage)
        {
            if (RuleApiSettings.AreAuthCredentialsConfigured())
                return true;

            ClearMenuSelection();
            ShowSettingsView();

            if (showMessage)
            {
                MessageBox.Show(
                    "App.config icindeki AuthUsername ve AuthPassword bos.\n\n" +
                    "Servisleri kullanmadan once Ayarlar ekranindan kullanici adi ve parola bilgilerini doldurun.",
                    "Eksik Kullanici Bilgileri",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return false;
        }

        private void ModelInfo_Checked(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ModelInfoView(currentModelInfo);
        }

        //private void ModelValidation_Checked(object sender, RoutedEventArgs e)
        //{
        //    MainContent.Content = new ModelValidationView();
        //}

        //private void ValidationRules_Checked(object sender, RoutedEventArgs e)
        //{
        //    MainContent.Content = new ValidationRulesView();
        //}

        //private void Settings_Checked(object sender, RoutedEventArgs e)
        //{
        //    MainContent.Content = new SettingsView();
        //}


        public void PopulateModels()
        {
            models = veloxapEDGErwinLib.getModelsNamePath() ?? new List<(string value, string key1, string key2)>();

            var modelItems = models
                .Select(model => new ModelSelection(model.value, model.key1, model.key2))
                .ToList();

            cmbMainModel.ItemsSource = modelItems;

            if (modelItems.Count > 0)
                cmbMainModel.SelectedIndex = 0;
            else
                MainContent.Content = new ModelInfoView();
            //comboBox1.Items.Clear();
            //models = veloxapEDGErwinLib.getModelsNamePath();



            //comboBox1.DataSource = models;
            //comboBox1.DisplayMember = "value";




            //if (comboBox1.Items.Count > 0)
            //    comboBox1.SelectedIndex = 0;

        }

        private async void CmbMainModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedModel = cmbMainModel.SelectedItem as ModelSelection;
            if (veloxapEDGErwinLib == null || selectedModel == null)
                return;

            selectedModelName = selectedModel.Name;
            selectedModelLongId = selectedModel.ObjectId;
            SetSelectedModelRuleParameters(selectedModel.Name);
            ClearValidationRules();

            currentModelInfo = veloxapEDGErwinLib.loadModelObject(
                selectedModel.ObjectId,
                selectedModel.PersistenceObjectId);

            if (rbModelInfo.IsChecked == true)
            {
                MainContent.Content = new ModelInfoView(currentModelInfo);
            }
            else if (rbTableUdps.IsChecked == true)
            {
                MainContent.Content = new ModelUdpView(
                    currentModelInfo,
                    oApp,
                    GetCurrentPersistenceUnit());
            }
            //else if (rbRules.IsChecked == true)
            //{
            //    if (!EnsureAuthCredentialsConfigured(showMessage: true))
            //        return;

            //    await LoadValidationRulesForSelectedModelAsync(showErrors: true);
            //    MainContent.Content = new ModelValidationView(
            //        currentModelInfo,
            //        rules,
            //        GetRuleService(),
            //        selectedModelName,
            //        selectedModelLongId,
            //        validationRules,
            //        true);
            //}
            else if (rbValidation.IsChecked == true)
            {
                if (!EnsureAuthCredentialsConfigured(showMessage: true))
                    return;

                await LoadValidationRulesForSelectedModelAsync(showErrors: true);
                MainContent.Content = new ModelValidationView(
                    currentModelInfo,
                    rules,
                    GetRuleService(),
                    selectedModelName,
                    selectedModelLongId,
                    validationRules,
                    false);
            }
        }

        private async Task<bool> LoadValidationRulesForSelectedModelAsync(bool showErrors)
        {
            EnsureRuleCollections();

            if (string.IsNullOrWhiteSpace(selectedModelName) || string.IsNullOrWhiteSpace(selectedModelLongId))
            {
                lastRuleRequestTrace = BuildRuleTraceMessage("Model parametreleri bos.", null);
                ApiTraceLogger.Info(lastRuleRequestTrace);
                ClearValidationRules();
                return false;
            }

            string currentModelKey = selectedModelName + "|" + selectedModelLongId;
            if (string.Equals(loadedRulesModelKey, currentModelKey, StringComparison.OrdinalIgnoreCase))
            {
                lastRuleRequestTrace = BuildRuleTraceMessage("Cache kullanildi.", null);
                return true;
            }

            ClearValidationRules();
            string serviceUrl = null;

            try
            {
                serviceUrl = BuildRulesByModelUrl(
                    RuleApiSettings.GetRulesByModelUrl(),
                    selectedModelName,
                    selectedModelLongId);

                lastRuleRequestTrace = BuildRuleTraceMessage("Istek gonderiliyor.", serviceUrl);
                ApiTraceLogger.Info(lastRuleRequestTrace);

                List<Rule> serviceRules = await GetRuleService().GetRulesAsync(serviceUrl);

                validationRules.AddRange(serviceRules ?? new List<Rule>());
                rules.AddRange(validationRules
                    .Select(rule => rule.RuleText)
                    .Where(ruleText => !string.IsNullOrWhiteSpace(ruleText)));

                loadedRulesModelKey = currentModelKey;
                lastRuleRequestTrace = BuildRuleTraceMessage(
                    "Istek tamamlandi. Kural sayisi: " + validationRules.Count,
                    serviceUrl);
                ApiTraceLogger.Info(lastRuleRequestTrace);
                return true;
            }
            catch (Exception ex)
            {
                ClearValidationRules();
                lastRuleRequestTrace = BuildRuleTraceMessage("Istek hatasi: " + ex.Message, serviceUrl);
                ApiTraceLogger.Error(lastRuleRequestTrace, ex);

                if (showErrors)
                {
                    MessageBox.Show(
                        "Validasyon Erisim Sorunu!\nModele ait kural bulunamadi!\n\n" + ex.Message,
                        "Validasyon Kurallari",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return false;
            }
        }

        private void ClearValidationRules()
        {
            EnsureRuleCollections();
            rules.Clear();
            validationRules.Clear();
            loadedRulesModelKey = null;
        }

        private void EnsureRuleCollections()
        {
            if (rules == null)
                rules = new List<string>();

            if (validationRules == null)
                validationRules = new List<Rule>();
        }

        private async Task InitializeAuthenticationAsync()
        {
            try
            {
                await GetAuthTokenProvider().GetTokenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Servis login islemi basarisiz.\n\n" + ex.Message,
                    "Servis Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private RuleService GetRuleService()
        {
            if (ruleService == null)
                ruleService = new RuleService(CreateAuthorizedHttpClient(
                    GetAuthTokenProvider(),
                    GetApiCookieContainer()));

            return ruleService;
        }

        private SCAPI.PersistenceUnit GetCurrentPersistenceUnit()
        {
            int selectedIndex = cmbMainModel == null
                ? -1
                : cmbMainModel.SelectedIndex;

            if (veloxapEDGErwinLib == null || selectedIndex < 0)
                return null;

            return veloxapEDGErwinLib.getPersistenceUnit(selectedIndex);
        }

        private AuthTokenProvider GetAuthTokenProvider()
        {
            if (authTokenProvider == null)
                authTokenProvider = new AuthTokenProvider(CreatePlainHttpClient(GetApiCookieContainer()));

            return authTokenProvider;
        }

        private CookieContainer GetApiCookieContainer()
        {
            if (apiCookieContainer == null)
                apiCookieContainer = new CookieContainer();

            return apiCookieContainer;
        }

        private static HttpClient CreatePlainHttpClient(CookieContainer cookieContainer)
        {
            return new HttpClient(CreateHttpClientHandler(cookieContainer));
        }

        private static HttpClient CreateAuthorizedHttpClient(
            AuthTokenProvider tokenProvider,
            CookieContainer cookieContainer)
        {
            return new HttpClient(new BearerTokenHandler(
                tokenProvider,
                CreateHttpClientHandler(cookieContainer)));
        }

        private static HttpClientHandler CreateHttpClientHandler(CookieContainer cookieContainer)
        {
            return new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookieContainer ?? new CookieContainer()
            };
        }

        private static string BuildRulesByModelUrl(string baseUrl, string modelName, string modelLongId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = RuleApiSettings.GetRulesByModelUrl();

            string separator = baseUrl.Contains("?")
                ? (baseUrl.EndsWith("?") || baseUrl.EndsWith("&") ? string.Empty : "&")
                : "?";

            return baseUrl
                + separator
                + "cName="
                + Uri.EscapeDataString(modelName ?? string.Empty)
                + "&cLongId="
                + Uri.EscapeDataString(modelLongId ?? string.Empty);
        }

        private string BuildRuleTraceMessage(string status, string serviceUrl)
        {
            var selectedModel = cmbMainModel == null ? null : cmbMainModel.SelectedItem as ModelSelection;
            string rawValue = selectedModel == null ? string.Empty : selectedModel.Name;

            var builder = new StringBuilder();
            builder.AppendLine(status ?? string.Empty);
            builder.AppendLine("LogFile: " + ApiTraceLogger.LogFilePath);
            builder.AppendLine("RulesUrl: " + (serviceUrl ?? string.Empty));
            builder.AppendLine("Parsed cName: " + (selectedModelName ?? string.Empty));
            builder.AppendLine("Parsed version: " + (selectedModelVersionNo ?? string.Empty));
            builder.AppendLine("Parsed cLongId: " + (selectedModelLongId ?? string.Empty));
            builder.AppendLine("Selected raw: " + rawValue);

            return builder.ToString();
        }

        private void SetSelectedModelRuleParameters(string selectedModelValue)
        {
            if (string.IsNullOrWhiteSpace(selectedModelValue))
                return;

            string versionNo = ExtractSelectedModelQueryValue(selectedModelValue, "version");
            string modelLongId = ExtractSelectedModelQueryValue(selectedModelValue, "modelLongId");

            if (!string.IsNullOrWhiteSpace(versionNo))
            {
                selectedModelVersionNo = versionNo;
                selectedModelName = "Version " + versionNo;
            }
            else
            {
                string parsedModelName = ExtractSelectedModelName(selectedModelValue);
                if (!string.IsNullOrWhiteSpace(parsedModelName))
                    selectedModelName = parsedModelName;
            }

            if (!string.IsNullOrWhiteSpace(modelLongId))
                selectedModelLongId = modelLongId;

            ApiTraceLogger.Info(
                "SELECTED MODEL PARSE" + Environment.NewLine +
                "SelectedRaw: " + selectedModelValue + Environment.NewLine +
                "ParsedVersion: " + (selectedModelVersionNo ?? string.Empty) + Environment.NewLine +
                "ParsedRuleName: " + (selectedModelName ?? string.Empty) + Environment.NewLine +
                "ParsedModelLongId: " + (selectedModelLongId ?? string.Empty));
        }

        private static string ExtractSelectedModelQueryValue(string selectedModelValue, string key)
        {
            if (string.IsNullOrWhiteSpace(selectedModelValue) || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string marker = key + "=";
            int start = selectedModelValue.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return string.Empty;

            start += marker.Length;

            int end = selectedModelValue.IndexOfAny(new[] { '&', ')', ',', ' ', '\r', '\n', '\t' }, start);
            if (end < 0)
                end = selectedModelValue.Length;

            if (end <= start)
                return string.Empty;

            return Uri.UnescapeDataString(selectedModelValue.Substring(start, end - start).Trim());
        }

        private static string ExtractSelectedModelName(string selectedModelValue)
        {
            if (string.IsNullOrWhiteSpace(selectedModelValue))
                return string.Empty;

            int queryIndex = selectedModelValue.IndexOf('?');
            if (queryIndex <= 0)
                return string.Empty;

            string beforeQuery = selectedModelValue.Substring(0, queryIndex);
            int lastSlash = beforeQuery.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash == beforeQuery.Length - 1)
                return string.Empty;

            return beforeQuery.Substring(lastSlash + 1).Trim();
        }

        private static string GetDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            int parenIndex = name.IndexOf('(');
            if (parenIndex <= 0)
                return name.Trim();

            return name.Substring(0, parenIndex).TrimEnd();
        }

        private sealed class ModelSelection
        {
            public ModelSelection(string name, string objectId, string persistenceObjectId)
            {
                Name = name;
                DisplayName = GetDisplayName(name);
                ObjectId = objectId;
                PersistenceObjectId = persistenceObjectId;
            }

            public string Name { get; }

            public string DisplayName { get; }

            public string ObjectId { get; }

            public string PersistenceObjectId { get; }
        }

    }
}
