using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
        private const int MinimumSearchLength = 3;
        private const int SearchDelayMilliseconds = 500;

        private readonly ModelInfo modelInfo;
        private readonly SCAPI.Application application;
        private readonly SCAPI.PersistenceUnit persistenceUnit;
        private readonly List<UdpRow> allRows;
        private readonly List<UdpTreeNode> tableNodes;
        private readonly ObservableCollection<UdpDetailRow> selectedDetails;
        private readonly DispatcherTimer searchTimer;

        private int tableCount;
        private string lastAppliedFilter;
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
            : this(modelInfo, application, persistenceUnit, null, null, null)
        {
        }

        internal ModelUdpView(
            ModelInfo modelInfo,
            SCAPI.Application application,
            SCAPI.PersistenceUnit persistenceUnit,
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            this.modelInfo = modelInfo;
            this.application = application;
            this.persistenceUnit = persistenceUnit;
            allRows = new List<UdpRow>();
            tableNodes = new List<UdpTreeNode>();
            selectedDetails = new ObservableCollection<UdpDetailRow>();
            searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds)
            };
            searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();

            treeUdp.ItemsSource = tableNodes;
            gridUdpDetails.ItemsSource = selectedDetails;

            UpdateSummaryCounts();
            ShowDetails(null);
            SetStatus(CanUseLazyScapi()
                ? "Tablolar yukleniyor..."
                : "UDP'ler yukleniyor...", false);

            Loaded += ModelUdpView_Loaded;
            SetLoading(true);
        }

        private bool CanUseLazyScapi()
        {
            return application != null && persistenceUnit != null;
        }

        private async void ModelUdpView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (hasStartedLoading)
                    return;

                hasStartedLoading = true;
                await ReloadRowsAsync("Tablolar yukleniyor...", false);
            }
            catch (Exception)
            {

            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
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
            catch (Exception)
            {

            }
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                searchTimer.Stop();
                ApplyFilter();
            }
            catch (Exception)
            {
            }
        }

        private async void TreeUdp_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                var node = e.NewValue as UdpTreeNode;
                if (node == null || node.IsPlaceholder)
                    return;

                ShowDetails(node);

                if (node.Row == null)
                {
                    await LoadTableNodeAsync(node);
                    ShowDetails(node);
                    return;
                }

                await PreviewSelectedUdpAsync(node.Row);
            }
            catch (Exception)
            {
            }
        }

        private void TreeUdpItem_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private async void TreeUdpItem_Expanded(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = e.OriginalSource as TreeViewItem;
                if (item == null)
                    return;

                var node = item.DataContext as UdpTreeNode;
                if (node == null || node.IsPlaceholder || node.Row != null)
                    return;

                await LoadTableNodeAsync(node);
            }
            catch (Exception)
            {
            }
        }

        private void ApplyFilter()
        {
            ApplyFilter(null, false);
        }

        private void ApplyFilter(TreeState treeState, bool preserveSelection)
        {
            try
            {
                string filter = txtSearch == null
    ? string.Empty
    : (txtSearch.Text ?? string.Empty).Trim();

                bool hasShortSearch = filter.Length > 0 && filter.Length < MinimumSearchLength;
                string activeFilter = filter.Length >= MinimumSearchLength
                    ? filter
                    : string.Empty;

                if (CanUseLazyScapi())
                {
                    ApplyLazyFilter(activeFilter, hasShortSearch);
                    lastAppliedFilter = activeFilter;
                    return;
                }

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
            catch (Exception)
            {
            }
        }

        private void ApplyLazyFilter(string activeFilter, bool hasShortSearch)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(activeFilter))
                {
                    treeUdp.ItemsSource = tableNodes;
                    UpdateLazyFilterStatus(tableNodes.Count, hasShortSearch, activeFilter);
                    return;
                }

                var filteredNodes = new List<UdpTreeNode>();
                foreach (var tableNode in tableNodes)
                {
                    if (Contains(tableNode.TableName, activeFilter))
                    {
                        filteredNodes.Add(tableNode);
                        continue;
                    }

                    var matchingRows = allRows
                        .Where(row =>
                            string.Equals(row.TableObjectId, tableNode.TableObjectId, StringComparison.OrdinalIgnoreCase) &&
                            Contains(row.SearchText, activeFilter))
                        .ToList();

                    if (matchingRows.Count == 0)
                        continue;

                    filteredNodes.Add(UdpTreeNode.CreateGroup(
                        tableNode.TableName + " (" + matchingRows.Count + ")",
                        0,
                        "Tablo",
                        tableNode.TableName,
                        tableNode.TableObjectId,
                        string.Join(", ", matchingRows.Select(row => row.DisplayUdpName)),
                        matchingRows.Count,
                        tableNode.NodeKey,
                        true,
                        false,
                        () => BuildUdpLeafNodes(matchingRows, null)));
                }

                treeUdp.ItemsSource = filteredNodes;
                UpdateLazyFilterStatus(filteredNodes.Count, hasShortSearch, activeFilter);
            }
            catch (Exception)
            {
            }
        }

        private void UpdateLazyFilterStatus(int visibleTables, bool hasShortSearch, string activeFilter)
        {
            try
            {

                txtVisibleCount.Text = visibleTables.ToString();
                emptyState.Visibility = visibleTables == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                txtEmpty.Text = tableNodes.Count == 0
                    ? "Secili modelde tablo bulunamadi."
                    : "Arama kriterine uygun tablo veya yuklenmis UDP bulunamadi.";

                if (hasShortSearch)
                {
                    SetStatus(
                        "Arama icin en az " + MinimumSearchLength +
                        " karakter girin. " + visibleTables + " tablo listeleniyor.",
                        false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(activeFilter))
                {
                    SetStatus(
                        tableCount + " tablo listeleniyor. UDP detaylari tablo acildikca yuklenecek.",
                        false);
                    return;
                }

                SetStatus(visibleTables + " tablo/sonuc bulundu.", false);
            }
            catch (Exception)
            {

            }
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
            try
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
            catch (Exception)
            {
            }
        }

        private static UdpTreeNode FindNodeByKey(IEnumerable<UdpTreeNode> nodes, string nodeKey)
        {
            try
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
            catch (Exception)
            {
                return null;
            }
        }

        private static List<UdpTreeNode> BuildTree(
            IList<UdpRow> rows,
            TreeState treeState,
            bool expandSearchResults)
        {
            var nodes = new List<UdpTreeNode>();

            try
            {

                if (rows == null || rows.Count == 0)
                    return nodes;

                foreach (var tableGroup in rows.GroupBy(row => row.TableName)
                                               .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    List<UdpRow> tableRows = tableGroup.ToList();
                    string tableObjectId = tableRows
                        .Select(row => row.TableObjectId)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                    string nodeKey = BuildTableNodeKey(tableObjectId, tableGroup.Key);
                    string udpNames = string.Join(
                        ", ",
                        tableRows
                            .Select(row => row.DisplayUdpName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

                    var tableNode = UdpTreeNode.CreateGroup(
                        tableGroup.Key + " (" + tableRows.Count + ")",
                        0,
                        "Tablo",
                        tableGroup.Key,
                        tableObjectId,
                        udpNames,
                        tableRows.Count,
                        nodeKey,
                        ShouldExpandNode(nodeKey, treeState, expandSearchResults),
                        ShouldSelectNode(nodeKey, treeState),
                        () => BuildUdpLeafNodes(tableRows, treeState));

                    nodes.Add(tableNode);
                }
            }
            catch (Exception)
            {

            }

            return nodes;
        }

        private static List<UdpTreeNode> BuildUdpLeafNodes(
            IEnumerable<UdpRow> rows,
            TreeState treeState)
        {
            var nodes = new List<UdpTreeNode>();
            try
            {

                if (rows == null)
                    return nodes;

                foreach (var row in rows.OrderBy(item => item.DisplayUdpName, StringComparer.OrdinalIgnoreCase))
                {
                    string nodeKey = BuildUdpNodeKey(row);
                    nodes.Add(UdpTreeNode.CreateLeaf(
                        row,
                        nodeKey,
                        ShouldSelectNode(nodeKey, treeState)));
                }
            }
            catch (Exception)
            {

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
            try
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
            catch (Exception)
            {

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

        private static string BuildTableNodeKey(string tableObjectId, string tableName)
        {
            return "T|" +
                   Safe(tableObjectId, string.Empty) +
                   "|" +
                   Safe(tableName, string.Empty);
        }

        private static string BuildUdpNodeKey(UdpRow row)
        {
            if (row == null)
                return string.Empty;

            return "U|" +
                   Safe(row.TableObjectId, string.Empty) +
                   "|" +
                   Safe(row.TableName, string.Empty) +
                   "|" +
                   Safe(row.UdpName, string.Empty);
        }

        private void ShowDetails(UdpTreeNode node)
        {
            try
            {
                selectedDetails.Clear();

                if (node == null)
                {
                    txtDetailTitle.Text = "UDP Detaylari";
                    AddDetail("Secim", "Soldaki agactan bir tablo veya UDP secin.");
                    return;
                }

                txtDetailTitle.Text = node.Row == null
                    ? node.TableName
                    : node.Title;

                if (node.Row == null)
                {
                    AddDetail("Tablo", node.TableName);

                    if (!node.ChildrenLoaded)
                    {
                        AddDetail("Durum", "UDP detaylari henuz yuklenmedi.");
                        AddDetail("Islem", "Tabloyu acinca veya secince detaylar yuklenir.");
                        return;
                    }

                    AddDetail("UDP Sayisi", node.Count.ToString());
                    AddDetail("UDP'ler", node.UdpNames);
                    return;
                }

                AddDetail("Tablo", node.Row.TableName);
                AddDetail("UDP", node.Row.DisplayUdpName);
                AddDetail("Deger", node.Row.Value);

            }
            catch (Exception)
            {

            }
        }

        private async Task LoadTableNodeAsync(UdpTreeNode node)
        {
            if (node == null ||
                node.IsPlaceholder ||
                node.Row != null ||
                node.ChildrenLoaded ||
                node.IsLoadingChildren ||
                !CanUseLazyScapi())
            {
                return;
            }

            node.SetLoadingChildren();
            SetBusyIndicator(true);
            SetStatus(node.TableName + " UDP detaylari yukleniyor...", false);
            await Task.Yield();

            try
            {
                ModelObject table = await RunScapiAsync(() =>
                    new ModelLoad(application).loadTableUdpObject(
                        persistenceUnit,
                        node.TableObjectId,
                        node.TableName));

                List<UdpRow> tableRows = BuildRowsForTable(table);
                foreach (var row in tableRows)
                    row.TableModelObject = table;

                allRows.RemoveAll(row =>
                    string.Equals(row.TableObjectId, node.TableObjectId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.TableName, node.TableName, StringComparison.OrdinalIgnoreCase));
                allRows.AddRange(tableRows);

                node.Count = tableRows.Count;
                node.UdpNames = string.Join(
                    ", ",
                    tableRows
                        .Select(row => row.DisplayUdpName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                node.Title = node.TableName + " (" + tableRows.Count + ")";
                node.TableModelObject = table;
                node.SetChildren(BuildUdpLeafNodes(tableRows, null));

                UpdateSummaryCounts();
                ShowDetails(node);
                SetStatus(node.TableName + " icin " + tableRows.Count + " UDP yuklendi.", false);
            }
            catch (Exception ex)
            {
                node.SetChildren(new List<UdpTreeNode>());
                SetStatus(node.TableName + " UDP detaylari yuklenemedi: " + ex.Message, true);
            }
            finally
            {
                SetBusyIndicator(false);
            }
        }

        private async Task PreviewSelectedUdpAsync(UdpRow row)
        {
            if (row == null)
                return;

            string targetUdpKey = ResolveTargetUdpKey(row.UdpName);
            if (string.IsNullOrWhiteSpace(targetUdpKey))
                return;

            string rowKey = BuildUdpNodeKey(row);
            SetBusyIndicator(true);
            SetStatus(row.DisplayUdpName + " hesaplaniyor...", false);
            await Task.Yield();

            try
            {
                ModelInfo previewModelInfo = ResolvePreviewModelInfo(row);
                TableUdpCalculationPreview preview = await Task.Run(() =>
                    TableUdpSecurityService.PreviewCalculation(
                        previewModelInfo,
                        row.TableObjectId,
                        row.TableName,
                        targetUdpKey));

                var selectedNode = treeUdp == null
                    ? null
                    : treeUdp.SelectedItem as UdpTreeNode;

                if (selectedNode != null &&
                    selectedNode.Row != null &&
                    string.Equals(BuildUdpNodeKey(selectedNode.Row), rowKey, StringComparison.Ordinal))
                {
                    AddPreviewDetails(preview);
                }

                SetStatus(row.DisplayUdpName + " hesaplandi.", false);
            }
            catch (Exception ex)
            {
                SetStatus(row.DisplayUdpName + " hesaplanamadi: " + ex.Message, true);
            }
            finally
            {
                SetBusyIndicator(false);
            }
        }

        private ModelInfo ResolvePreviewModelInfo(UdpRow row)
        {
            if (row == null || row.TableModelObject == null)
                return modelInfo;

            var lazyModelInfo = new ModelInfo();
            lazyModelInfo.setoModelObject(new List<ModelObject> { row.TableModelObject });
            return lazyModelInfo;
        }

        private static string ResolveTargetUdpKey(string propertyName)
        {
            string normalizedName = NormalizeUdpName(propertyName);

            if (string.IsNullOrWhiteSpace(normalizedName) ||
                normalizedName.EndsWith(
                    "sirkapsamindakiveridegeri",
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (normalizedName.EndsWith("veridegeri", StringComparison.OrdinalIgnoreCase))
                return "veridegeri";

            if (normalizedName.EndsWith("bankagorecedegeri", StringComparison.OrdinalIgnoreCase))
                return "bankagorecedegeri";

            if (normalizedName.EndsWith("guvenliksinifidegeri", StringComparison.OrdinalIgnoreCase))
                return "guvenliksinifidegeri";

            return string.Empty;
        }

        private void AddDetail(string property, string value)
        {
            selectedDetails.Add(new UdpDetailRow
            {
                Property = string.IsNullOrWhiteSpace(property) ? "-" : property,
                Value = string.IsNullOrWhiteSpace(value) ? "-" : value
            });
        }

        private void AddPreviewDetails(TableUdpCalculationPreview preview)
        {
            if (preview == null)
                return;

            if (!string.IsNullOrWhiteSpace(preview.FormulaText))
                AddDetail("Formul", preview.FormulaText);

            if (!string.IsNullOrWhiteSpace(preview.CalculatedValue))
                AddDetail("Hesaplanan Deger", preview.CalculatedValue);

            if (!string.IsNullOrWhiteSpace(preview.Message))
                AddDetail("Hesaplama", preview.Message);
        }

        private async Task ReloadRowsAsync(string loadingMessage, bool preserveTreeState)
        {
            if (searchTimer != null)
                searchTimer.Stop();

            TreeState treeState = preserveTreeState
                ? CaptureTreeState()
                : null;

            SetLoading(true);
            SetStatus(loadingMessage, false);

            try
            {
                if (CanUseLazyScapi())
                    await ReloadTableSummariesAsync();
                else
                    await ReloadRowsFromModelInfoAsync(treeState, preserveTreeState);
            }
            catch (Exception ex)
            {
                allRows.Clear();
                tableNodes.Clear();
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

        private async Task ReloadTableSummariesAsync()
        {
            List<ModelObject> tables = await RunScapiAsync(() =>
                new ModelLoad(application).loadTableSummaries(persistenceUnit));

            allRows.Clear();
            tableNodes.Clear();

            foreach (var table in tables
                .Where(item => item != null)
                .OrderBy(item => item.getoName(), StringComparer.OrdinalIgnoreCase))
            {
                string tableName = Safe(table.getoName(), "(adsiz tablo)");
                string tableObjectId = Safe(table.getoObjectId(), string.Empty);

                tableNodes.Add(UdpTreeNode.CreateLazyGroup(
                    tableName,
                    0,
                    "Tablo",
                    tableName,
                    tableObjectId,
                    string.Empty,
                    0,
                    BuildTableNodeKey(tableObjectId, tableName),
                    false,
                    false));
            }

            tableCount = tableNodes.Count;
            treeUdp.ItemsSource = tableNodes;
            UpdateSummaryCounts();
            ShowDetails(null);
            UpdateLazyFilterStatus(tableNodes.Count, false, string.Empty);
        }

        private async Task ReloadRowsFromModelInfoAsync(
            TreeState treeState,
            bool preserveTreeState)
        {
            if (modelInfo == null)
            {
                allRows.Clear();
                tableCount = 0;
                UpdateSummaryCounts();
                ApplyFilter(treeState, preserveTreeState);
                return;
            }

            UdpRowsBuildResult result = await Task.Run(() => BuildRows(modelInfo));

            allRows.Clear();
            allRows.AddRange(result.Rows);
            tableCount = result.TableCount;

            UpdateSummaryCounts();
            ApplyFilter(treeState, preserveTreeState);
        }

        private void UpdateSummaryCounts()
        {
            if (txtTableCount != null)
                txtTableCount.Text = tableCount.ToString();

            if (txtUdpCount != null)
                txtUdpCount.Text = allRows.Count.ToString();
        }

        private void SetLoading(bool value)
        {
            isLoading = value;

            if (txtSearch != null)
                txtSearch.IsEnabled = !value;

            if (treeUdp != null)
                treeUdp.IsEnabled = !value;

            SetBusyIndicator(value);
        }

        private void SetBusyIndicator(bool value)
        {
            Mouse.OverrideCursor = value ? Cursors.Wait : null;

            if (busyBar != null)
                busyBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
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
                result.Rows.AddRange(BuildRowsForTable(table));

            result.TableCount = result.Rows
                .Select(row => row.TableName)
                .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            result.Rows = result.Rows
                .OrderBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.UdpName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result;
        }

        private static List<UdpRow> BuildRowsForTable(ModelObject table)
        {
            var rows = new List<UdpRow>();

            if (table == null)
                return rows;

            string tableName = Safe(table.getoName(), "(adsiz tablo)");
            var properties = table.getoObjectProperty();

            if (properties == null)
                return rows;

            foreach (var property in properties)
            {
                if (!IsUdpProperty(property))
                    continue;

                rows.Add(CreateUdpRow(
                    Safe(table.getoObjectId(), string.Empty),
                    tableName,
                    Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                    Safe(property.getoPropertyValue(), string.Empty),
                    table));
            }

            return rows
                .OrderBy(row => row.DisplayUdpName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static UdpRow CreateUdpRow(
            string tableObjectId,
            string tableName,
            string udpName,
            string value,
            ModelObject tableModelObject)
        {
            var row = new UdpRow
            {
                TableObjectId = tableObjectId,
                TableName = tableName,
                UdpName = udpName,
                DisplayUdpName = BuildDisplayUdpName(udpName),
                Value = value,
                TableModelObject = tableModelObject
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
                    row.DisplayUdpName,
                    row.UdpName,
                    row.Value
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildDisplayUdpName(string udpName)
        {
            if (string.IsNullOrWhiteSpace(udpName))
                return udpName;

            int index = udpName.LastIndexOf('.');
            if (index < 0 || index == udpName.Length - 1)
                return udpName;

            return udpName.Substring(index + 1);
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

        private static bool IsUdpProperty(ObjectProperty property)
        {
            if (property == null)
                return false;

            string propertyName = property.getoPropertyClassName();
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            return IsTargetTableUdp(propertyName);
        }

        private static bool IsTargetTableUdp(string propertyName)
        {
            return !string.IsNullOrWhiteSpace(ResolveTargetUdpKey(propertyName));
        }

        private static string NormalizeUdpName(string propertyName)
        {
            return string.IsNullOrWhiteSpace(propertyName)
                ? string.Empty
                : propertyName
                    .Replace("_", string.Empty)
                    .Replace(" ", string.Empty)
                    .ToLowerInvariant();
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

        private static Task<T> RunScapiAsync<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>();
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(action == null ? default(T) : action());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return completion.Task;
        }

        public sealed class UdpRow
        {
            public string TableObjectId { get; set; }

            public string TableName { get; set; }

            public string UdpName { get; set; }

            public string DisplayUdpName { get; set; }

            public string Value { get; set; }

            public string SearchText { get; set; }

            internal ModelObject TableModelObject { get; set; }
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

    public sealed class UdpTreeNode : INotifyPropertyChanged
    {
        private Func<List<UdpTreeNode>> childFactory;
        private bool childrenLoaded;
        private string title;
        private string udpNames;
        private int count;
        private bool isExpanded;
        private bool isSelected;
        private bool isLoadingChildren;

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

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title
        {
            get { return title; }
            set
            {
                if (title == value)
                    return;

                title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        public Thickness IndentMargin { get; set; }

        public FontWeight FontWeight { get; set; }

        public Brush Foreground { get; set; }

        public string NodeType { get; set; }

        public string TableName { get; set; }

        public string TableObjectId { get; set; }

        public string ObjectName { get; set; }

        public string UdpNames
        {
            get { return udpNames; }
            set
            {
                if (udpNames == value)
                    return;

                udpNames = value;
                OnPropertyChanged(nameof(UdpNames));
            }
        }

        public int Count
        {
            get { return count; }
            set
            {
                if (count == value)
                    return;

                count = value;
                OnPropertyChanged(nameof(Count));
            }
        }

        public string NodeKey { get; set; }

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (isExpanded == value)
                    return;

                isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected == value)
                    return;

                isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public ModelUdpView.UdpRow Row { get; set; }

        internal ModelObject TableModelObject { get; set; }

        public ObservableCollection<UdpTreeNode> Children { get; private set; }

        public bool IsPlaceholder { get; private set; }

        public bool ChildrenLoaded
        {
            get { return childrenLoaded; }
        }

        public bool IsLoadingChildren
        {
            get { return isLoadingChildren; }
        }

        public static UdpTreeNode CreateLazyGroup(
            string title,
            double leftMargin,
            string nodeType,
            string tableName,
            string tableObjectId,
            string udpNames,
            int count,
            string nodeKey,
            bool isExpanded,
            bool isSelected)
        {
            var node = CreateGroup(
                title,
                leftMargin,
                nodeType,
                tableName,
                tableObjectId,
                udpNames,
                count,
                nodeKey,
                isExpanded,
                isSelected,
                null);

            node.childrenLoaded = false;
            node.Children.Clear();
            node.Children.Add(CreatePlaceholder());
            return node;
        }

        public static UdpTreeNode CreateGroup(
            string title,
            double leftMargin,
            string nodeType,
            string tableName,
            string tableObjectId,
            string udpNames,
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
                TableObjectId = tableObjectId,
                ObjectName = string.Empty,
                UdpNames = udpNames,
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
                Title = row == null ? string.Empty : row.DisplayUdpName,
                IndentMargin = new Thickness(12, 0, 0, 0),
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                NodeType = "UDP",
                TableName = row == null ? string.Empty : row.TableName,
                TableObjectId = row == null ? string.Empty : row.TableObjectId,
                ObjectName = string.Empty,
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

            OnPropertyChanged(nameof(ChildrenLoaded));
        }

        public void SetLoadingChildren()
        {
            if (childrenLoaded)
                return;

            isLoadingChildren = true;
            Children.Clear();
            Children.Add(CreatePlaceholder());
            OnPropertyChanged(nameof(IsLoadingChildren));
        }

        public void SetChildren(IEnumerable<UdpTreeNode> children)
        {
            childrenLoaded = true;
            isLoadingChildren = false;
            childFactory = null;
            Children.Clear();

            foreach (var child in children ?? Enumerable.Empty<UdpTreeNode>())
                Children.Add(child);

            OnPropertyChanged(nameof(ChildrenLoaded));
            OnPropertyChanged(nameof(IsLoadingChildren));
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

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
