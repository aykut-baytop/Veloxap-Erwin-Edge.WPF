using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VeloxapEDGEWpfLib.Services;

namespace VeloxapEDGEWpfLib.Pages
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private const string AuthUsernameKey = "AuthUsername";
        private const string AuthPasswordKey = "AuthPassword";
        private const string MissingAuthCredentialsMessage = "Kullanici adi ve parola bilgilerini doldurun.";

        private List<AppConfigSetting> currentUserSettings;
        private List<AppConfigSetting> currentServiceSettings;

        public SettingsView()
        {
            InitializeComponent();
            LoadAppSettings(null);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveAppSettings();
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            LoadAppSettings("Ayarlar yeniden yuklendi.");
        }

        private void LoadAppSettings(string statusMessage)
        {
            try
            {
                Configuration config = OpenAssemblyConfiguration();
                List<AppConfigSetting> settings = BuildSettingList(config);
                currentUserSettings = settings
                    .Where(setting => IsUserSetting(setting.Key))
                    .ToList();
                currentServiceSettings = settings
                    .Where(setting => !IsUserSetting(setting.Key))
                    .ToList();

                dgUserSettings.ItemsSource = currentUserSettings;
                dgServiceSettings.ItemsSource = currentServiceSettings;
                txtUserSettingCount.Text = currentUserSettings.Count + " ayar";
                txtServiceSettingCount.Text = currentServiceSettings.Count + " ayar";
                txtSettingCount.Text = settings.Count + " ayar";
                txtConfigSource.Text = "";
                emptyState.Visibility = settings.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                SetStatusForLoadedSettings(statusMessage);
            }
            catch (Exception ex)
            {
                currentUserSettings = new List<AppConfigSetting>();
                currentServiceSettings = new List<AppConfigSetting>();
                dgUserSettings.ItemsSource = currentUserSettings;
                dgServiceSettings.ItemsSource = currentServiceSettings;
                txtUserSettingCount.Text = "0 ayar";
                txtServiceSettingCount.Text = "0 ayar";
                txtSettingCount.Text = "0 ayar";
                txtConfigSource.Text = "App.config okunamadi.";
                emptyState.Visibility = Visibility.Visible;
                SetStatus("Ayarlar okunamadi: " + ex.Message, true);
            }
        }

        private void SaveAppSettings()
        {
            try
            {
                CommitSettingsGrid(dgUserSettings);
                CommitSettingsGrid(dgServiceSettings);

                Configuration config = OpenAssemblyConfiguration();
                KeyValueConfigurationCollection appSettings = config.AppSettings.Settings;

                foreach (AppConfigSetting setting in GetCurrentSettings())
                {
                    if (setting == null || string.IsNullOrWhiteSpace(setting.Key))
                        continue;

                    string value = setting.Value ?? string.Empty;

                    if (appSettings[setting.Key] == null)
                        appSettings.Add(setting.Key, value);
                    else
                        appSettings[setting.Key].Value = value;
                }

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                LoadAppSettings("Ayarlar kaydedildi.");
            }
            catch (Exception ex)
            {
                SetStatus("Ayarlar kaydedilemedi: " + ex.Message, true);
            }
        }

        private static void CommitSettingsGrid(DataGrid dataGrid)
        {
            dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            dataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private IEnumerable<AppConfigSetting> GetCurrentSettings()
        {
            return (currentUserSettings ?? new List<AppConfigSetting>())
                .Concat(currentServiceSettings ?? new List<AppConfigSetting>());
        }

        private static Configuration OpenAssemblyConfiguration()
        {
            return ConfigurationManager.OpenExeConfiguration(typeof(SettingsView).Assembly.Location);
        }

        private static List<AppConfigSetting> BuildSettingList(Configuration config)
        {
            var result = new List<AppConfigSetting>();
            var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            KeyValueConfigurationCollection appSettings = config.AppSettings.Settings;

            foreach (KeyValuePair<string, string> defaultSetting in RuleApiSettings.GetDefaultAppSettings())
            {
                string value = ReadSettingValue(appSettings, defaultSetting.Key);

                result.Add(new AppConfigSetting(
                    defaultSetting.Key,
                    string.IsNullOrWhiteSpace(value) ? defaultSetting.Value : value));
                addedKeys.Add(defaultSetting.Key);
            }

            foreach (string key in appSettings.AllKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Where(key => !addedKeys.Contains(key))
                .OrderBy(key => key))
            {
                result.Add(new AppConfigSetting(key, appSettings[key].Value));
                addedKeys.Add(key);
            }

            return result;
        }

        private static string ReadSettingValue(KeyValueConfigurationCollection appSettings, string key)
        {
            KeyValueConfigurationElement setting = appSettings[key];
            if (setting != null)
                return setting.Value;

            try
            {
                return ConfigurationManager.AppSettings[key];
            }
            catch (ConfigurationErrorsException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsUserSetting(string key)
        {
            return string.Equals(key, AuthUsernameKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, AuthPasswordKey, StringComparison.OrdinalIgnoreCase);
        }

        private void SetStatusForLoadedSettings(string statusMessage)
        {
            if (HasMissingAuthCredentials(currentUserSettings))
            {
                SetStatus(MissingAuthCredentialsMessage, true);
                return;
            }

            SetStatus(statusMessage, false);
        }

        private static bool HasMissingAuthCredentials(IEnumerable<AppConfigSetting> settings)
        {
            bool hasUsername = false;
            bool hasPassword = false;

            foreach (AppConfigSetting setting in settings ?? Enumerable.Empty<AppConfigSetting>())
            {
                if (setting == null)
                    continue;

                if (string.Equals(setting.Key, AuthUsernameKey, StringComparison.OrdinalIgnoreCase))
                    hasUsername = !string.IsNullOrWhiteSpace(setting.Value);
                else if (string.Equals(setting.Key, AuthPasswordKey, StringComparison.OrdinalIgnoreCase))
                    hasPassword = !string.IsNullOrWhiteSpace(setting.Value);
            }

            return !hasUsername || !hasPassword;
        }

        private void SetStatus(string message, bool isError)
        {
            txtSaveStatus.Text = message ?? string.Empty;
            txtSaveStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
                : new SolidColorBrush(Color.FromRgb(5, 150, 105));
        }

        private sealed class AppConfigSetting
        {
            public AppConfigSetting(string key, string value)
            {
                Key = key;
                Value = value;
            }

            public string Key { get; private set; }

            public string Value { get; set; }
        }
    }
}
