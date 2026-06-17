using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VeloxapEDGEWpfLib.Models;
using VeloxapEDGEWpfLib.Services;

namespace VeloxapEDGEWpfLib.Pages
{
    public partial class ModelUdpView : UserControl
    {
        private readonly ModelInfo modelInfo;
        private readonly SCAPI.Application application;
        private readonly SCAPI.PersistenceUnit persistenceUnit;
        private readonly List<UdpRow> allRows;
        private readonly ObservableCollection<UdpRow> visibleRows;
        private bool isBusy;

        public ModelUdpView()
            : this(null, null, null)
        {
        }

        internal ModelUdpView(ModelInfo modelInfo)
            : this(modelInfo, null, null)
        {
        }

        internal ModelUdpView(
            ModelInfo modelInfo,
            SCAPI.Application application,
            SCAPI.PersistenceUnit persistenceUnit)
        {
            this.modelInfo = modelInfo;
            this.application = application;
            this.persistenceUnit = persistenceUnit;
            allRows = new List<UdpRow>();
            visibleRows = new ObservableCollection<UdpRow>();

            InitializeComponent();

            allRows.AddRange(BuildRows(modelInfo));
            gridUdp.ItemsSource = visibleRows;

            txtModelName.Text = BuildModelText(modelInfo);
            txtTableCount.Text = CountTables(modelInfo).ToString();
            txtUdpCount.Text = allRows.Count.ToString();

            ApplyFilter();
            UpdateCalculateButton();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnCalculateSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (modelInfo == null || application == null || persistenceUnit == null)
            {
                MessageBox.Show(
                    "Varlik degeri hesaplama icin secili erwin modeli hazir degil.",
                    "Varlik Degeri Hesaplama",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int calculableTableCount = TableUdpSecurityService.CountCalculableTables(modelInfo);
            if (calculableTableCount == 0)
            {
                SetStatus("Hesaplanabilir Varlik_Degeri UDP kaydi bulunamadi.", true);
                MessageBox.Show(
                    "Erisebilirlik, Butunluk, Gizlilik_Seviyesi ve Varlik_Degeri UDP alanlari tam olan tablo bulunamadi.",
                    "Varlik Degeri Hesaplama",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                BuildCalculationConfirmationMessage(calculableTableCount),
                "Varlik Degeri Hesaplama",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            SetBusy(true);
            SetStatus("Varlik degerleri hesaplaniyor...", false);

            try
            {
                var service = new TableUdpSecurityService(
                    application,
                    persistenceUnit);

                TableUdpSecurityApplyResult result = service.Apply(modelInfo);

                allRows.Clear();
                allRows.AddRange(BuildRows(modelInfo));
                txtUdpCount.Text = allRows.Count.ToString();
                ApplyFilter();

                bool hasError = result.FailedTables > 0;
                SetStatus(result.ToSummary(), hasError);

                MessageBox.Show(
                    BuildApplyResultMessage(result),
                    "Varlik Degeri Hesaplama",
                    MessageBoxButton.OK,
                    hasError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Varlik degeri hesaplama hatasi: " + ex.Message, true);
                MessageBox.Show(
                    "Varlik degeri hesaplama tamamlanamadi.\n\n" + ex.Message,
                    "Varlik Degeri Hesaplama",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplyFilter()
        {
            string filter = txtSearch == null
                ? string.Empty
                : (txtSearch.Text ?? string.Empty).Trim();

            visibleRows.Clear();

            IEnumerable<UdpRow> rows = allRows;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                rows = rows.Where(row => Contains(row.TableName, filter) ||
                                         Contains(row.UdpName, filter) ||
                                         Contains(row.Value, filter) ||
                                         Contains(row.AsString, filter));
            }

            foreach (var row in rows)
                visibleRows.Add(row);

            txtVisibleCount.Text = visibleRows.Count.ToString();

            bool isEmpty = visibleRows.Count == 0;
            emptyState.Visibility = isEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtEmpty.Text = allRows.Count == 0
                ? "Secili model tablolarinda UDP bulunamadi."
                : "Arama kriterine uygun UDP bulunamadi.";

            txtStatus.Text = allRows.Count == 0
                ? "UDP bulunamadi."
                : visibleRows.Count + " UDP listeleniyor.";
        }

        private void SetBusy(bool value)
        {
            isBusy = value;
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
            UpdateCalculateButton();
        }

        private void UpdateCalculateButton()
        {
            if (btnCalculateSecurity == null)
                return;

            btnCalculateSecurity.IsEnabled =
                !isBusy &&
                modelInfo != null &&
                application != null &&
                persistenceUnit != null;
        }

        private void SetStatus(string message, bool isError)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
                : new SolidColorBrush(Color.FromRgb(55, 65, 81));
        }

        private static List<UdpRow> BuildRows(ModelInfo modelInfo)
        {
            var rows = new List<UdpRow>();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            if (objects == null)
                return rows;

            foreach (var table in EnumerateTables(objects))
            {
                string tableName = Safe(table.getoName(), "(adsiz tablo)");
                var properties = table.getoObjectProperty();

                if (properties == null)
                    continue;

                foreach (var property in properties.Where(IsUdpProperty))
                {
                    rows.Add(new UdpRow
                    {
                        TableName = tableName,
                        UdpName = Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                        Value = Safe(property.getoPropertyValue(), string.Empty),
                        AsString = Safe(property.getoPropertyFormatAsString(), string.Empty),
                        DataType = Safe(property.getoPropertyType(), string.Empty)
                    });
                }
            }

            return rows
                .OrderBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.UdpName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<ModelObject> EnumerateTables(IEnumerable<ModelObject> objects)
        {
            if (objects == null)
                yield break;

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                if (string.Equals(obj.getoClassName(), "Entity", StringComparison.OrdinalIgnoreCase))
                    yield return obj;

                var children = obj.getoModelObject();
                if (children == null)
                    continue;

                foreach (var child in EnumerateTables(children))
                    yield return child;
            }
        }

        private static int CountTables(ModelInfo modelInfo)
        {
            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            return EnumerateTables(objects).Count();
        }

        private static bool IsUdpProperty(ObjectProperty property)
        {
            if (property == null)
                return false;

            string propertyName = property.getoPropertyClassName();
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            return propertyName.Contains(".") ||
                   propertyName.IndexOf("UDP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   propertyName.IndexOf("User Defined", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildModelText(ModelInfo modelInfo)
        {
            if (modelInfo == null)
                return "Secili model yuklenmedi.";

            string name = Safe(modelInfo.getoName(), "Secili model");
            string location = Safe(modelInfo.getoLocation(), string.Empty);

            return string.IsNullOrWhiteSpace(location)
                ? name
                : name + " - " + location;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
        }

        private static string BuildCalculationConfirmationMessage(int calculableTableCount)
        {
            return calculableTableCount + " tablo icin Varlik_Degeri UDP alani guncellenecek.\n\n" +
                   "Formul:\n" +
                   "Erisebilirlik, Butunluk ve Gizlilik_Seviyesi listelerindeki secimler 1-4 arasinda seviyeye cevrilir.\n" +
                   "Listenin en alt itemi seviye 1 kabul edilir; yukariya dogru +1 ilerler.\n" +
                   "Sonuc = Yuvarla((Erisebilirlik + Butunluk + Gizlilik_Seviyesi) / 3)\n\n" +
                   "4-\u00c7ok Gizli/Y\u00fcksek\n" +
                   "3-Gizli/Orta\n" +
                   "2-Hizmete \u00d6zel/D\u00fc\u015f\u00fck\n" +
                   "1-Kamuya A\u00e7\u0131k/Bilgi\n\n" +
                   "Devam edilsin mi?";
        }

        private static string BuildApplyResultMessage(TableUdpSecurityApplyResult result)
        {
            if (result == null)
                return string.Empty;

            string message = result.ToSummary();

            if (result.Messages == null || result.Messages.Count == 0)
                return message;

            return message + Environment.NewLine + Environment.NewLine +
                   string.Join(
                       Environment.NewLine,
                       result.Messages.Take(8));
        }

        public sealed class UdpRow
        {
            public string TableName { get; set; }

            public string UdpName { get; set; }

            public string Value { get; set; }

            public string AsString { get; set; }

            public string DataType { get; set; }
        }
    }
}
