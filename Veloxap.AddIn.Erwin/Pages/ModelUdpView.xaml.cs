using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        private readonly ObservableCollection<UdpDetailRow> selectedDetails;
        private readonly DispatcherTimer searchTimer;
        private const double ActionsPanelCollapsedWidth = 66;
        private const double ActionsPanelExpandedWidth = 240;
        private const double ActionButtonExpandedWidth = 216;
        private const int MinimumSearchLength = 3;
        private const int SearchDelayMilliseconds = 500;
        private int tableCount;
        private string lastAppliedFilter;
        private bool isBusy;
        private bool isLoading;
        private bool hasStartedLoading;

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
            selectedDetails = new ObservableCollection<UdpDetailRow>();
            searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds)
            };
            searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();
            SetActionsPanelExpanded(false);

            treeUdp.ItemsSource = new List<UdpTreeNode>();
            gridUdpDetails.ItemsSource = selectedDetails;

            UpdateSummaryCounts();
            ShowDetails(null);
            SetStatus(
                modelInfo == null ? "UDP bulunamadi." : "UDP'ler yukleniyor...",
                false);

            Loaded += ModelUdpView_Loaded;
            SetLoading(modelInfo != null);
            UpdateCalculateButton();
        }

        private async void ModelUdpView_Loaded(object sender, RoutedEventArgs e)
        {
            if (hasStartedLoading)
                return;

            hasStartedLoading = true;
            await ReloadRowsAsync("UDP'ler yukleniyor...", false);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading)
                return;

            if (searchTimer == null)
            {
                ApplyFilter();
                return;
            }

            searchTimer.Stop();
            searchTimer.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            searchTimer.Stop();
            ApplyFilter();
        }

        private void TreeUdp_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var node = e.NewValue as UdpTreeNode;
            if (node != null && node.IsPlaceholder)
                return;

            ShowDetails(node);
        }

        private void TreeUdpItem_Loaded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null)
                return;

            var node = item.DataContext as UdpTreeNode;
            if (node == null || node.IsPlaceholder || !node.IsExpanded)
                return;

            node.EnsureChildrenLoaded();
        }

        private void TreeUdpItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = e.OriginalSource as TreeViewItem;
            if (item == null)
                return;

            var node = item.DataContext as UdpTreeNode;
            if (node == null || node.IsPlaceholder)
                return;

            node.EnsureChildrenLoaded();
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

        private async void BtnCalculateSecurity_Click(object sender, RoutedEventArgs e)
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
                    "Erisilebilirlik, Butunluk, Gizlilik_Seviyesi ve Veri_Degeri UDP alanlari tam olan tablo bulunamadi.",
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

                await ReloadRowsAsync("UDP listesi yenileniyor...", true);

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

        private async void BtnCalculateBankRelative_Click(object sender, RoutedEventArgs e)
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

                await ReloadRowsAsync("UDP listesi yenileniyor...", true);

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

        private async void BtnCalculateSecurityClass_Click(object sender, RoutedEventArgs e)
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

                await ReloadRowsAsync("UDP listesi yenileniyor...", true);

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
            ApplyFilter(null, false);
        }

        private void ApplyFilter(TreeState treeState, bool preserveSelection)
        {
            string filter = txtSearch == null
                ? string.Empty
                : (txtSearch.Text ?? string.Empty).Trim();

            bool hasShortSearch = filter.Length > 0 && filter.Length < MinimumSearchLength;
            string activeFilter = filter.Length >= MinimumSearchLength
                ? filter
                : string.Empty;

            if (hasShortSearch &&
                string.Equals(activeFilter, lastAppliedFilter, StringComparison.OrdinalIgnoreCase))
            {
                UpdateFilterStatus(allRows.Count, activeFilter, hasShortSearch);
                return;
            }

            IList<UdpRow> filteredRows = allRows;
            if (!string.IsNullOrWhiteSpace(activeFilter))
            {
                filteredRows = allRows
                    .Where(row => Contains(row.SearchText, activeFilter))
                    .ToList();
            }

            bool expandSearchResults = !string.IsNullOrWhiteSpace(activeFilter);
            var treeNodes = BuildTree(filteredRows, treeState, expandSearchResults);
            treeUdp.ItemsSource = treeNodes;

            RestoreSelectionDetails(treeState, treeNodes, filteredRows, preserveSelection);

            UpdateFilterStatus(filteredRows.Count, activeFilter, hasShortSearch);
            lastAppliedFilter = activeFilter;
        }

        private void UpdateFilterStatus(int filteredCount, string activeFilter, bool hasShortSearch)
        {
            txtVisibleCount.Text = filteredCount.ToString();

            bool isEmpty = filteredCount == 0;
            emptyState.Visibility = isEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtEmpty.Text = allRows.Count == 0
                ? "Secili model tablolarinda UDP bulunamadi."
                : "Arama kriterine uygun UDP bulunamadi.";

            if (allRows.Count == 0)
            {
                SetStatus("UDP bulunamadi.", false);
                return;
            }

            if (hasShortSearch)
            {
                SetStatus(
                    "Arama icin en az " + MinimumSearchLength +
                    " karakter girin. " + filteredCount + " UDP listeleniyor.",
                    false);
                return;
            }

            SetStatus(
                string.IsNullOrWhiteSpace(activeFilter)
                    ? filteredCount + " UDP listeleniyor."
                    : filteredCount + " UDP bulundu.",
                false);
        }

        private void RestoreSelectionDetails(
            TreeState treeState,
            IEnumerable<UdpTreeNode> treeNodes,
            IEnumerable<UdpRow> filteredRows,
            bool preserveSelection)
        {
            if (!preserveSelection ||
                treeState == null ||
                string.IsNullOrWhiteSpace(treeState.SelectedNodeKey))
            {
                ShowDetails(null);
                return;
            }

            UdpTreeNode selectedNode = FindNodeByKey(treeNodes, treeState.SelectedNodeKey);
            if (selectedNode != null)
            {
                selectedNode.IsSelected = true;
                ShowDetails(selectedNode);
                return;
            }

            UdpRow selectedRow = filteredRows == null
                ? null
                : filteredRows.FirstOrDefault(row =>
                    string.Equals(
                        BuildUdpNodeKey(row),
                        treeState.SelectedNodeKey,
                        StringComparison.Ordinal));

            ShowDetails(selectedRow == null
                ? null
                : UdpTreeNode.CreateLeaf(
                    selectedRow,
                    BuildUdpNodeKey(selectedRow),
                    true));
        }

        private static UdpTreeNode FindNodeByKey(IEnumerable<UdpTreeNode> nodes, string nodeKey)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(nodeKey))
                return null;

            foreach (var node in nodes)
            {
                if (node == null || node.IsPlaceholder)
                    continue;

                if (string.Equals(node.NodeKey, nodeKey, StringComparison.Ordinal))
                    return node;

                UdpTreeNode childNode = FindNodeByKey(node.Children, nodeKey);
                if (childNode != null)
                    return childNode;
            }

            return null;
        }

        private static List<UdpTreeNode> BuildTree(
            IList<UdpRow> rows,
            TreeState treeState,
            bool expandSearchResults)
        {
            var nodes = new List<UdpTreeNode>();

            if (rows == null || rows.Count == 0)
                return nodes;

            foreach (var tableGroup in rows.GroupBy(row => row.TableName)
                                           .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                List<UdpRow> tableRows = tableGroup.ToList();
                string nodeKey = BuildTableNodeKey(tableGroup.Key);
                var tableNode = UdpTreeNode.CreateGroup(
                    tableGroup.Key + " (" + tableRows.Count + ")",
                    0,
                    "Tablo",
                    tableGroup.Key,
                    string.Empty,
                    tableRows.Count,
                    nodeKey,
                    ShouldExpandNode(nodeKey, treeState, expandSearchResults),
                    ShouldSelectNode(nodeKey, treeState),
                    () => BuildObjectNodes(
                        tableGroup.Key,
                        tableRows,
                        treeState,
                        expandSearchResults));

                nodes.Add(tableNode);
            }

            return nodes;
        }

        private static List<UdpTreeNode> BuildObjectNodes(
            string tableName,
            IList<UdpRow> rows,
            TreeState treeState,
            bool expandSearchResults)
        {
            var nodes = new List<UdpTreeNode>();

            if (rows == null || rows.Count == 0)
                return nodes;

            foreach (var objectGroup in rows.GroupBy(BuildObjectGroupKey)
                                            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                List<UdpRow> objectRows = objectGroup.ToList();
                string nodeKey = BuildObjectNodeKey(tableName, objectGroup.Key);
                var objectNode = UdpTreeNode.CreateGroup(
                    objectGroup.Key + " (" + objectRows.Count + ")",
                    12,
                    "Nesne",
                    tableName,
                    objectGroup.Key,
                    objectRows.Count,
                    nodeKey,
                    ShouldExpandNode(nodeKey, treeState, expandSearchResults),
                    ShouldSelectNode(nodeKey, treeState),
                    () => BuildUdpLeafNodes(objectRows, treeState));

                nodes.Add(objectNode);
            }

            return nodes;
        }

        private static List<UdpTreeNode> BuildUdpLeafNodes(
            IEnumerable<UdpRow> rows,
            TreeState treeState)
        {
            var nodes = new List<UdpTreeNode>();

            if (rows == null)
                return nodes;

            foreach (var row in rows.OrderBy(item => item.UdpName, StringComparer.OrdinalIgnoreCase))
            {
                string nodeKey = BuildUdpNodeKey(row);
                nodes.Add(UdpTreeNode.CreateLeaf(
                    row,
                    nodeKey,
                    ShouldSelectNode(nodeKey, treeState)));
            }

            return nodes;
        }

        private TreeState CaptureTreeState()
        {
            var treeState = new TreeState();
            var nodes = treeUdp == null
                ? null
                : treeUdp.ItemsSource as IEnumerable<UdpTreeNode>;

            CaptureTreeState(nodes, treeState);
            return treeState;
        }

        private static void CaptureTreeState(IEnumerable<UdpTreeNode> nodes, TreeState treeState)
        {
            if (nodes == null || treeState == null)
                return;

            foreach (var node in nodes)
            {
                if (node == null || node.IsPlaceholder)
                    continue;

                if (!string.IsNullOrWhiteSpace(node.NodeKey))
                {
                    if (node.IsExpanded)
                        treeState.ExpandedNodeKeys.Add(node.NodeKey);

                    if (node.IsSelected)
                        treeState.SelectedNodeKey = node.NodeKey;
                }

                CaptureTreeState(node.Children, treeState);
            }
        }

        private static bool ShouldExpandNode(
            string nodeKey,
            TreeState treeState,
            bool expandSearchResults)
        {
            return expandSearchResults ||
                   (treeState != null &&
                    treeState.ExpandedNodeKeys.Contains(nodeKey));
        }

        private static bool ShouldSelectNode(string nodeKey, TreeState treeState)
        {
            return treeState != null &&
                   string.Equals(
                       treeState.SelectedNodeKey,
                       nodeKey,
                       StringComparison.Ordinal);
        }

        private static string BuildTableNodeKey(string tableName)
        {
            return "T|" + Safe(tableName, string.Empty);
        }

        private static string BuildObjectNodeKey(string tableName, string objectName)
        {
            return "O|" +
                   Safe(tableName, string.Empty) +
                   "|" +
                   Safe(objectName, string.Empty);
        }

        private static string BuildUdpNodeKey(UdpRow row)
        {
            if (row == null)
                return string.Empty;

            return "U|" +
                   Safe(row.TableName, string.Empty) +
                   "|" +
                   BuildObjectGroupKey(row) +
                   "|" +
                   Safe(row.UdpName, string.Empty);
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

        private async Task ReloadRowsAsync(string loadingMessage, bool preserveTreeState)
        {
            if (searchTimer != null)
                searchTimer.Stop();

            TreeState treeState = preserveTreeState
                ? CaptureTreeState()
                : null;

            if (modelInfo == null)
            {
                allRows.Clear();
                tableCount = 0;
                UpdateSummaryCounts();
                ApplyFilter(treeState, preserveTreeState);
                return;
            }

            SetLoading(true);
            SetStatus(loadingMessage, false);

            try
            {
                UdpRowsBuildResult result = await Task.Run(() => BuildRows(modelInfo));

                allRows.Clear();
                allRows.AddRange(result.Rows);
                tableCount = result.TableCount;

                UpdateSummaryCounts();
                ApplyFilter(treeState, preserveTreeState);
            }
            catch (Exception ex)
            {
                allRows.Clear();
                tableCount = 0;
                UpdateSummaryCounts();
                treeUdp.ItemsSource = new List<UdpTreeNode>();
                ShowDetails(null);
                SetStatus("UDP listesi yuklenemedi: " + ex.Message, true);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void UpdateSummaryCounts()
        {
            if (txtTableCount != null)
                txtTableCount.Text = tableCount.ToString();

            if (txtUdpCount != null)
                txtUdpCount.Text = allRows.Count.ToString();
        }

        private void SetBusy(bool value)
        {
            isBusy = value;
            UpdateBusyCursor();
            UpdateCalculateButton();
        }

        private void SetLoading(bool value)
        {
            isLoading = value;

            if (txtSearch != null)
                txtSearch.IsEnabled = !value;

            UpdateBusyCursor();
            UpdateCalculateButton();
        }

        private void UpdateBusyCursor()
        {
            Mouse.OverrideCursor = isBusy || isLoading ? Cursors.Wait : null;
        }

        private void UpdateCalculateButton()
        {
            bool isEnabled =
                !isBusy &&
                !isLoading &&
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

        private static UdpRowsBuildResult BuildRows(ModelInfo modelInfo)
        {
            var result = new UdpRowsBuildResult();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            if (objects == null)
                return result;

            foreach (var table in EnumerateTables(objects))
            {
                result.TableCount++;

                string tableName = Safe(table.getoName(), "(adsiz tablo)");
                var properties = table.getoObjectProperty();

                if (properties != null)
                {
                    foreach (var property in properties)
                    {
                        if (!IsUdpProperty(property))
                            continue;

                        result.Rows.Add(CreateUdpRow(
                            tableName,
                            "Tablo",
                            string.Empty,
                            Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                            Safe(property.getoPropertyValue(), string.Empty),
                            Safe(property.getoPropertyFormatAsString(), string.Empty),
                            Safe(property.getoPropertyType(), string.Empty)));
                    }
                }

                foreach (var column in EnumerateColumns(table))
                {
                    string columnName = Safe(column.getoName(), "(adsiz kolon)");
                    var columnProperties = column.getoObjectProperty();

                    if (columnProperties == null)
                        continue;

                    foreach (var property in columnProperties)
                    {
                        if (!IsUdpProperty(property))
                            continue;

                        result.Rows.Add(CreateUdpRow(
                            tableName,
                            "Kolon",
                            columnName,
                            Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                            Safe(property.getoPropertyValue(), string.Empty),
                            Safe(property.getoPropertyFormatAsString(), string.Empty),
                            Safe(property.getoPropertyType(), string.Empty)));
                    }
                }
            }

            result.Rows = result.Rows
                .OrderBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ObjectType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ColumnName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.UdpName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result;
        }

        private static UdpRow CreateUdpRow(
            string tableName,
            string objectType,
            string columnName,
            string udpName,
            string value,
            string asString,
            string dataType)
        {
            var row = new UdpRow
            {
                TableName = tableName,
                ObjectType = objectType,
                ColumnName = columnName,
                UdpName = udpName,
                Value = value,
                AsString = asString,
                DataType = dataType
            };

            row.SearchText = BuildSearchText(row);
            return row;
        }

        private static string BuildSearchText(UdpRow row)
        {
            if (row == null)
                return string.Empty;

            return string.Join(
                "\n",
                new[]
                {
                    row.TableName,
                    row.ObjectType,
                    row.ColumnName,
                    row.UdpName,
                    row.Value,
                    row.AsString
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
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
                "erisilebilirlik",
                "butunluk",
                "gizlilikseviyesi",
                "veridegeri",
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

            public string SearchText { get; set; }
        }

        public sealed class UdpDetailRow
        {
            public string Property { get; set; }

            public string Value { get; set; }
        }

        private sealed class UdpRowsBuildResult
        {
            public UdpRowsBuildResult()
            {
                Rows = new List<UdpRow>();
            }

            public List<UdpRow> Rows { get; set; }

            public int TableCount { get; set; }
        }

        private sealed class TreeState
        {
            public TreeState()
            {
                ExpandedNodeKeys = new HashSet<string>(StringComparer.Ordinal);
            }

            public HashSet<string> ExpandedNodeKeys { get; private set; }

            public string SelectedNodeKey { get; set; }
        }

    }

    public sealed class UdpTreeNode
    {
        private Func<List<UdpTreeNode>> childFactory;
        private bool childrenLoaded;

        public UdpTreeNode()
        {
            Children = new ObservableCollection<UdpTreeNode>();
            childrenLoaded = true;
        }

        private UdpTreeNode(Func<List<UdpTreeNode>> childFactory)
            : this()
        {
            this.childFactory = childFactory;
            childrenLoaded = childFactory == null;

            if (!childrenLoaded)
                Children.Add(CreatePlaceholder());
        }

        public string Title { get; set; }

        public Thickness IndentMargin { get; set; }

        public FontWeight FontWeight { get; set; }

        public Brush Foreground { get; set; }

        public string NodeType { get; set; }

        public string TableName { get; set; }

        public string ObjectName { get; set; }

        public int Count { get; set; }

        public string NodeKey { get; set; }

        public bool IsExpanded { get; set; }

        public bool IsSelected { get; set; }

        public ModelUdpView.UdpRow Row { get; set; }

        public ObservableCollection<UdpTreeNode> Children { get; private set; }

        public bool IsPlaceholder { get; private set; }

        public static UdpTreeNode CreateGroup(
            string title,
            double leftMargin,
            string nodeType,
            string tableName,
            string objectName,
            int count,
            string nodeKey,
            bool isExpanded,
            bool isSelected,
            Func<List<UdpTreeNode>> childFactory)
        {
            return new UdpTreeNode(childFactory)
            {
                Title = title,
                IndentMargin = new Thickness(leftMargin, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                NodeType = nodeType,
                TableName = tableName,
                ObjectName = objectName,
                Count = count,
                NodeKey = nodeKey,
                IsExpanded = isExpanded,
                IsSelected = isSelected
            };
        }

        public static UdpTreeNode CreateLeaf(
            ModelUdpView.UdpRow row,
            string nodeKey,
            bool isSelected)
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
                NodeKey = nodeKey,
                IsSelected = isSelected,
                Row = row
            };
        }

        public void EnsureChildrenLoaded()
        {
            if (childrenLoaded)
                return;

            childrenLoaded = true;
            Children.Clear();

            Func<List<UdpTreeNode>> factory = childFactory;
            childFactory = null;

            if (factory == null)
                return;

            foreach (var child in factory())
                Children.Add(child);
        }

        private static UdpTreeNode CreatePlaceholder()
        {
            return new UdpTreeNode
            {
                Title = "Yukleniyor...",
                IndentMargin = new Thickness(24, 0, 0, 0),
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                NodeType = "Yukleniyor",
                IsPlaceholder = true
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
