using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Services;

namespace Veloxap.AddIn.Erwin.Pages
{
    public partial class ModelUdpView : UserControl
    {
        private readonly ModelInfo modelInfo;
        private readonly SCAPI.Application application;
        private readonly SCAPI.PersistenceUnit persistenceUnit;
        private readonly List<UdpRow> allRows;
        private readonly ObservableCollection<UdpTreeNode> visibleTreeNodes;
        private readonly ObservableCollection<UdpDetailRow> selectedDetails;
        private const double ActionsPanelCollapsedWidth = 66;
        private const double ActionsPanelExpandedWidth = 240;
        private const double ActionButtonExpandedWidth = 216;
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
            visibleTreeNodes = new ObservableCollection<UdpTreeNode>();
            selectedDetails = new ObservableCollection<UdpDetailRow>();

            InitializeComponent();
            SetActionsPanelExpanded(false);

            allRows.AddRange(BuildRows(modelInfo));
            treeUdp.ItemsSource = visibleTreeNodes;
            gridUdpDetails.ItemsSource = selectedDetails;

            txtTableCount.Text = CountTables(modelInfo).ToString();
            txtUdpCount.Text = allRows.Count.ToString();

            ApplyFilter();
            UpdateCalculateButton();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void TreeUdp_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ShowDetails(e.NewValue as UdpTreeNode);
        }

        private void ActionsPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            SetActionsPanelExpanded(true);
        }

        private void ActionsPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            SetActionsPanelExpanded(false);
        }

        private void SetActionsPanelExpanded(bool value)
        {
            if (actionsColumn != null)
            {
                actionsColumn.Width = new GridLength(
                    value ? ActionsPanelExpandedWidth : ActionsPanelCollapsedWidth);
            }

            Visibility headerVisibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (txtActionsTitle != null)
                txtActionsTitle.Visibility = headerVisibility;

            if (txtActionsSubtitle != null)
                txtActionsSubtitle.Visibility = headerVisibility;

            SetActionButtonExpanded(btnCalculateSecurity, txtCalculateSecurity, value);
            SetActionButtonExpanded(btnCalculateBankRelative, txtCalculateBankRelative, value);
            SetActionButtonExpanded(btnCalculateSecurityClass, txtCalculateSecurityClass, value);
        }

        private static void SetActionButtonExpanded(Button button, TextBlock label, bool value)
        {
            if (button != null)
            {
                if (value)
                    button.Width = ActionButtonExpandedWidth;
                else
                    button.ClearValue(FrameworkElement.WidthProperty);
            }

            if (label != null)
            {
                if (value)
                    label.Visibility = Visibility.Visible;
                else
                    label.ClearValue(UIElement.VisibilityProperty);
            }
        }

        private void BtnCalculateSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (modelInfo == null || application == null || persistenceUnit == null)
            {
                MessageBox.Show(
                    "Veri degeri hesaplama icin secili erwin modeli hazir degil.",
                    "Veri Degeri Hesaplama",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int calculableTableCount = TableUdpSecurityService.CountCalculableTables(modelInfo);
            if (calculableTableCount == 0)
            {
                SetStatus("Hesaplanabilir Veri_Degeri UDP kaydi bulunamadi.", true);
                MessageBox.Show(
                    "Erisebilirlik, Butunluk, Gizlilik_Seviyesi ve Veri_Degeri UDP alanlari tam olan tablo bulunamadi.",
                    "Veri Degeri Hesaplama",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SetBusy(true);
            SetStatus("Veri degerleri hesaplaniyor...", false);

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
                    "Veri Degeri Hesaplama",
                    MessageBoxButton.OK,
                    hasError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Veri degeri hesaplama hatasi: " + ex.Message, true);
                MessageBox.Show(
                    "Veri degeri hesaplama tamamlanamadi.\n\n" + ex.Message,
                    "Veri Degeri Hesaplama",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnCalculateBankRelative_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Banka Gorece Degeri Hesaplama";

            if (modelInfo == null || application == null || persistenceUnit == null)
            {
                MessageBox.Show(
                    "Banka gorece degeri hesaplama icin secili erwin modeli hazir degil.",
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var service = new TableUdpSecurityService(
                application,
                persistenceUnit);

            int calculableTableCount = TableUdpSecurityService.CountBankRelativeCalculableTables(modelInfo);
            if (calculableTableCount == 0)
            {
                TableUdpSecurityApplyResult emptyResult = service.ApplyBankRelativeValue(modelInfo);
                SetStatus("Hesaplanabilir Banka_Gorece_Degeri UDP kaydi bulunamadi.", true);
                MessageBox.Show(
                    BuildApplyResultMessage(emptyResult),
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SetBusy(true);
            SetStatus("Banka gorece degerleri hesaplaniyor...", false);

            try
            {
                TableUdpSecurityApplyResult result = service.ApplyBankRelativeValue(modelInfo);

                allRows.Clear();
                allRows.AddRange(BuildRows(modelInfo));
                txtUdpCount.Text = allRows.Count.ToString();
                ApplyFilter();

                bool hasError = result.FailedTables > 0 || result.SkippedTables > 0;
                SetStatus(result.ToSummary(), hasError);

                MessageBox.Show(
                    BuildApplyResultMessage(result),
                    title,
                    MessageBoxButton.OK,
                    hasError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Banka gorece degeri hesaplama hatasi: " + ex.Message, true);
                MessageBox.Show(
                    "Banka gorece degeri hesaplama tamamlanamadi.\n\n" + ex.Message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnCalculateSecurityClass_Click(object sender, RoutedEventArgs e)
        {
            const string title = "Guvenlik Sinifi Degeri Hesaplama";

            if (modelInfo == null || application == null || persistenceUnit == null)
            {
                MessageBox.Show(
                    "Guvenlik sinifi degeri hesaplama icin secili erwin modeli hazir degil.",
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var service = new TableUdpSecurityService(
                application,
                persistenceUnit);

            int calculableTableCount = TableUdpSecurityService.CountSecurityClassCalculableTables(modelInfo);
            if (calculableTableCount == 0)
            {
                TableUdpSecurityApplyResult emptyResult = service.ApplySecurityClassValue(modelInfo);
                SetStatus("Hesaplanabilir Guvenlik_Sinifi_Degeri UDP kaydi bulunamadi.", true);
                MessageBox.Show(
                    BuildApplyResultMessage(emptyResult),
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SetBusy(true);
            SetStatus("Guvenlik sinifi degerleri hesaplaniyor...", false);

            try
            {
                TableUdpSecurityApplyResult result = service.ApplySecurityClassValue(modelInfo);

                allRows.Clear();
                allRows.AddRange(BuildRows(modelInfo));
                txtUdpCount.Text = allRows.Count.ToString();
                ApplyFilter();

                bool hasError = result.FailedTables > 0 || result.SkippedTables > 0;
                SetStatus(result.ToSummary(), hasError);

                MessageBox.Show(
                    BuildApplyResultMessage(result),
                    title,
                    MessageBoxButton.OK,
                    hasError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Guvenlik sinifi degeri hesaplama hatasi: " + ex.Message, true);
                MessageBox.Show(
                    "Guvenlik sinifi degeri hesaplama tamamlanamadi.\n\n" + ex.Message,
                    title,
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

            visibleTreeNodes.Clear();

            IEnumerable<UdpRow> rows = allRows;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                rows = rows.Where(row => Contains(row.TableName, filter) ||
                                         Contains(row.ObjectType, filter) ||
                                         Contains(row.ColumnName, filter) ||
                                         Contains(row.UdpName, filter) ||
                                         Contains(row.Value, filter) ||
                                         Contains(row.AsString, filter));
            }

            var filteredRows = rows.ToList();
            foreach (var node in BuildTree(filteredRows))
                visibleTreeNodes.Add(node);

            ShowDetails(null);

            txtVisibleCount.Text = filteredRows.Count.ToString();

            bool isEmpty = filteredRows.Count == 0;
            emptyState.Visibility = isEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtEmpty.Text = allRows.Count == 0
                ? "Secili model tablolarinda UDP bulunamadi."
                : "Arama kriterine uygun UDP bulunamadi.";

            txtStatus.Text = allRows.Count == 0
                ? "UDP bulunamadi."
                : filteredRows.Count + " UDP listeleniyor.";
        }

        private static List<UdpTreeNode> BuildTree(IEnumerable<UdpRow> rows)
        {
            var nodes = new List<UdpTreeNode>();

            if (rows == null)
                return nodes;

            foreach (var tableGroup in rows.GroupBy(row => row.TableName)
                                           .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var tableNode = UdpTreeNode.CreateGroup(
                    tableGroup.Key + " (" + tableGroup.Count() + ")",
                    0,
                    "Tablo",
                    tableGroup.Key,
                    string.Empty,
                    tableGroup.Count());

                foreach (var objectGroup in tableGroup.GroupBy(BuildObjectGroupKey)
                                                      .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var objectNode = UdpTreeNode.CreateGroup(
                        objectGroup.Key + " (" + objectGroup.Count() + ")",
                        12,
                        "Nesne",
                        tableGroup.Key,
                        objectGroup.Key,
                        objectGroup.Count());

                    foreach (var row in objectGroup.OrderBy(item => item.UdpName, StringComparer.OrdinalIgnoreCase))
                        objectNode.Children.Add(UdpTreeNode.CreateLeaf(row));

                    tableNode.Children.Add(objectNode);
                }

                nodes.Add(tableNode);
            }

            return nodes;
        }

        private static string BuildObjectGroupKey(UdpRow row)
        {
            if (row == null)
                return string.Empty;

            if (string.Equals(row.ObjectType, "Tablo", StringComparison.OrdinalIgnoreCase))
                return "Tablo";

            return string.IsNullOrWhiteSpace(row.ColumnName)
                ? Safe(row.ObjectType, "Nesne")
                : row.ObjectType + ": " + row.ColumnName;
        }

        private void ShowDetails(UdpTreeNode node)
        {
            selectedDetails.Clear();

            if (node == null)
            {
                txtDetailTitle.Text = "UDP Detaylari";
                AddDetail("Secim", "Soldaki agactan bir tablo, nesne veya UDP secin.");
                return;
            }

            txtDetailTitle.Text = node.Title;

            if (node.Row == null)
            {
                AddDetail("Tip", node.NodeType);
                AddDetail("Tablo", node.TableName);
                AddDetail("Nesne", node.ObjectName);
                AddDetail("UDP Sayisi", node.Count.ToString());
                return;
            }

            AddDetail("Tablo", node.Row.TableName);
            AddDetail("Nesne", node.Row.ObjectType);
            AddDetail("Kolon", node.Row.ColumnName);
            AddDetail("UDP", node.Row.UdpName);
            AddDetail("Deger", node.Row.Value);
            AddDetail("As String", node.Row.AsString);
            AddDetail("Tip", node.Row.DataType);
        }

        private void AddDetail(string property, string value)
        {
            selectedDetails.Add(new UdpDetailRow
            {
                Property = property,
                Value = string.IsNullOrWhiteSpace(value) ? "-" : value
            });
        }

        private void SetBusy(bool value)
        {
            isBusy = value;
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
            UpdateCalculateButton();
        }

        private void UpdateCalculateButton()
        {
            bool isEnabled =
                !isBusy &&
                modelInfo != null &&
                application != null &&
                persistenceUnit != null;

            if (btnCalculateSecurity != null)
                btnCalculateSecurity.IsEnabled = isEnabled;

            if (btnCalculateBankRelative != null)
                btnCalculateBankRelative.IsEnabled = isEnabled;

            if (btnCalculateSecurityClass != null)
                btnCalculateSecurityClass.IsEnabled = isEnabled;
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

                if (properties != null)
                {
                    foreach (var property in properties.Where(IsUdpProperty))
                    {
                        rows.Add(new UdpRow
                        {
                            TableName = tableName,
                            ObjectType = "Tablo",
                            ColumnName = string.Empty,
                            UdpName = Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                            Value = Safe(property.getoPropertyValue(), string.Empty),
                            AsString = Safe(property.getoPropertyFormatAsString(), string.Empty),
                            DataType = Safe(property.getoPropertyType(), string.Empty)
                        });
                    }
                }

                foreach (var column in EnumerateColumns(table))
                {
                    string columnName = Safe(column.getoName(), "(adsiz kolon)");
                    var columnProperties = column.getoObjectProperty();

                    if (columnProperties == null)
                        continue;

                    foreach (var property in columnProperties.Where(IsUdpProperty))
                    {
                        rows.Add(new UdpRow
                        {
                            TableName = tableName,
                            ObjectType = "Kolon",
                            ColumnName = columnName,
                            UdpName = Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                            Value = Safe(property.getoPropertyValue(), string.Empty),
                            AsString = Safe(property.getoPropertyFormatAsString(), string.Empty),
                            DataType = Safe(property.getoPropertyType(), string.Empty)
                        });
                    }
                }
            }

            return rows
                .OrderBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ObjectType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ColumnName, StringComparer.OrdinalIgnoreCase)
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

        private static IEnumerable<ModelObject> EnumerateColumns(ModelObject table)
        {
            if (table == null)
                yield break;

            var children = table.getoModelObject();
            if (children == null)
                yield break;

            foreach (var child in children)
            {
                if (child == null)
                    continue;

                if (string.Equals(child.getoClassName(), "Attribute", StringComparison.OrdinalIgnoreCase))
                    yield return child;

                foreach (var nestedChild in EnumerateColumns(child))
                    yield return nestedChild;
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
                   propertyName.IndexOf("User Defined", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   IsKnownSecurityUdp(propertyName);
        }

        private static bool IsKnownSecurityUdp(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            string normalizedName = propertyName
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

            string[] knownNames =
            {
                "erisebilirlik",
                "erisilebilirlik",
                "butunluk",
                "gizlilikseviyesi",
                "veridegeri",
                "varlikdegeri",
                "issureciseviyesi",
                "bankagorecedegeri",
                "kisiselverimi",
                "hassasverimi",
                "guvenliksinifidegeri"
            };

            return knownNames.Any(name => normalizedName.EndsWith(name));
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
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

            public string ObjectType { get; set; }

            public string ColumnName { get; set; }

            public string UdpName { get; set; }

            public string Value { get; set; }

            public string AsString { get; set; }

            public string DataType { get; set; }
        }

        public sealed class UdpDetailRow
        {
            public string Property { get; set; }

            public string Value { get; set; }
        }

    }

    public sealed class UdpTreeNode
    {
        public UdpTreeNode()
        {
            Children = new ObservableCollection<UdpTreeNode>();
        }

        public string Title { get; set; }

        public Thickness IndentMargin { get; set; }

        public FontWeight FontWeight { get; set; }

        public Brush Foreground { get; set; }

        public string NodeType { get; set; }

        public string TableName { get; set; }

        public string ObjectName { get; set; }

        public int Count { get; set; }

        public ModelUdpView.UdpRow Row { get; set; }

        public ObservableCollection<UdpTreeNode> Children { get; private set; }

        public static UdpTreeNode CreateGroup(
            string title,
            double leftMargin,
            string nodeType,
            string tableName,
            string objectName,
            int count)
        {
            return new UdpTreeNode
            {
                Title = title,
                IndentMargin = new Thickness(leftMargin, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                NodeType = nodeType,
                TableName = tableName,
                ObjectName = objectName,
                Count = count
            };
        }

        public static UdpTreeNode CreateLeaf(ModelUdpView.UdpRow row)
        {
            return new UdpTreeNode
            {
                Title = row == null ? string.Empty : row.UdpName,
                IndentMargin = new Thickness(24, 0, 0, 0),
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                NodeType = "UDP",
                TableName = row == null ? string.Empty : row.TableName,
                ObjectName = row == null ? string.Empty : BuildObjectName(row),
                Count = 1,
                Row = row
            };
        }

        private static string BuildObjectName(ModelUdpView.UdpRow row)
        {
            if (row == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(row.ColumnName)
                ? row.ObjectType
                : row.ObjectType + ": " + row.ColumnName;
        }
    }
}
