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
        private readonly RuleService catalogRuleService;
        private readonly string catalogName;
        private readonly string catalogLongId;
        private readonly List<UdpRow> allRows;
        private readonly ObservableCollection<UdpDetailRow> selectedDetails;
        private readonly ObservableCollection<ApprovalStepDisplayItem> approvalStepItems;
        private readonly DispatcherTimer searchTimer;
        private const int MinimumSearchLength = 3;
        private const int SearchDelayMilliseconds = 500;
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
            catalogRuleService = ruleService;
            this.catalogName = catalogName;
            this.catalogLongId = catalogLongId;
            allRows = new List<UdpRow>();
            selectedDetails = new ObservableCollection<UdpDetailRow>();
            approvalStepItems = new ObservableCollection<ApprovalStepDisplayItem>();
            searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds)
            };
            searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();

            treeUdp.ItemsSource = new List<UdpTreeNode>();
            gridUdpDetails.ItemsSource = selectedDetails;
            approvalSteps.ItemsSource = approvalStepItems;

            UpdateSummaryCounts();
            ResetApprovalHistory();
            ShowDetails(null);
            SetStatus(
                modelInfo == null ? "UDP bulunamadi." : "UDP'ler yukleniyor...",
                false);

            Loaded += ModelUdpView_Loaded;
            SetLoading(modelInfo != null);
        }

        private async void ModelUdpView_Loaded(object sender, RoutedEventArgs e)
        {
            if (hasStartedLoading)
                return;

            hasStartedLoading = true;
            Task approvalTask = LoadApprovalHistoryAsync();
            await ReloadRowsAsync("UDP'ler yukleniyor...", false);
            await approvalTask;
        }

        private async Task LoadApprovalHistoryAsync()
        {
            ResetApprovalHistory();

            if (catalogRuleService == null)
            {
                SetApprovalStatus("Servis baglantisi hazir degil.", string.Empty, true);
                return;
            }

            if (string.IsNullOrWhiteSpace(catalogName) || string.IsNullOrWhiteSpace(catalogLongId))
            {
                SetApprovalStatus("cName veya cLongId okunamadi.", string.Empty, true);
                return;
            }

            try
            {
                CatalogApprovalStatus approvalStatus = await catalogRuleService.GetApprovalStatusByCatalogAsync(
                    RuleApiSettings.GetApprovalStatusByCatalogUrl(),
                    catalogName,
                    catalogLongId);

                approvalStepItems.Clear();

                foreach (CatalogApprovalStep step in approvalStatus == null
                    ? Enumerable.Empty<CatalogApprovalStep>()
                    : approvalStatus.Steps)
                {
                    approvalStepItems.Add(new ApprovalStepDisplayItem
                    {
                        StepText = BuildApprovalStepText(step),
                        DetailText = BuildApprovalStepDetailText(step)
                    });
                }

                SetApprovalStatus(
                    BuildApprovalStatusText(approvalStatus),
                    approvalStatus == null ? string.Empty : approvalStatus.Message,
                    false);
            }
            catch (Exception ex)
            {
                approvalStepItems.Clear();
                SetApprovalStatus("Onay durumu okunamadi.", ex.Message, true);
            }
        }

        private void ResetApprovalHistory()
        {
            approvalStepItems.Clear();
            SetApprovalStatus("Kontrol ediliyor...", string.Empty, false);
        }

        private void SetApprovalStatus(string status, string message, bool isError)
        {
            if (txtApprovalStatus != null)
            {
                txtApprovalStatus.Text = string.IsNullOrWhiteSpace(status) ? "-" : status;
                txtApprovalStatus.Foreground = isError
                    ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
                    : new SolidColorBrush(Color.FromRgb(17, 24, 39));
            }

            if (txtApprovalMessage != null)
                txtApprovalMessage.Text = message ?? string.Empty;
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

        private async void TreeUdp_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var node = e.NewValue as UdpTreeNode;
            if (node != null && node.IsPlaceholder)
                return;

            ShowDetails(node);

            if (node == null || node.Row == null)
                return;

            await PreviewSelectedUdpAsync(node.Row);
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
                    string.Empty,
                    udpNames,
                    tableRows.Count,
                    nodeKey,
                    ShouldExpandNode(nodeKey, treeState, expandSearchResults),
                    ShouldSelectNode(nodeKey, treeState),
                    () => BuildUdpLeafNodes(tableRows, treeState));

                nodes.Add(tableNode);
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

            foreach (var row in rows.OrderBy(item => item.DisplayUdpName, StringComparer.OrdinalIgnoreCase))
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
            selectedDetails.Clear();

            if (node == null)
            {
                txtDetailTitle.Text = "UDP Detaylari";
                AddDetail("Secim", "Soldaki agactan bir tablo veya UDP secin.");
                return;
            }

            txtDetailTitle.Text = node.Title;

            if (node.Row == null)
            {
                AddDetail("Tablo", node.TableName);
                AddDetail("UDP Sayisi", node.Count.ToString());
                AddDetail("UDP'ler", node.UdpNames);
                return;
            }

            AddDetail("Tablo", node.Row.TableName);
            AddDetail("UDP", node.Row.DisplayUdpName);
            AddDetail("Deger", node.Row.Value);
        }

        private async Task PreviewSelectedUdpAsync(UdpRow row)
        {
            if (row == null || isLoading)
                return;

            string targetUdpKey = ResolveTargetUdpKey(row.UdpName);
            if (string.IsNullOrWhiteSpace(targetUdpKey))
                return;

            string rowKey = BuildUdpNodeKey(row);
            SetLoading(true);
            SetStatus(row.DisplayUdpName + " hesaplaniyor...", false);
            await Task.Yield();

            try
            {
                TableUdpCalculationPreview preview = await Task.Run(() =>
                    TableUdpSecurityService.PreviewCalculation(
                        modelInfo,
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
                SetLoading(false);
            }
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
                Property = property,
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

        private static string BuildApprovalStatusText(CatalogApprovalStatus approvalStatus)
        {
            if (approvalStatus == null)
                return "Onay bilgisi bulunamadi.";

            if (!string.IsNullOrWhiteSpace(approvalStatus.Status) &&
                !string.IsNullOrWhiteSpace(approvalStatus.StepText))
            {
                return approvalStatus.Status + " - " + approvalStatus.StepText;
            }

            if (!string.IsNullOrWhiteSpace(approvalStatus.Status))
                return approvalStatus.Status;

            if (approvalStatus.Steps != null && approvalStatus.Steps.Count > 0)
                return approvalStatus.Steps.Count + " onay step'i";

            return "Onay bilgisi bulunamadi.";
        }

        private static string BuildApprovalStepText(CatalogApprovalStep step)
        {
            if (step == null)
                return string.Empty;

            string stepName = string.IsNullOrWhiteSpace(step.StepName)
                ? "Step " + step.StepNumber
                : step.StepName;

            string status = string.IsNullOrWhiteSpace(step.Status)
                ? string.Empty
                : " - " + step.Status;

            return step.StepNumber + ". " + stepName + status;
        }

        private static string BuildApprovalStepDetailText(CatalogApprovalStep step)
        {
            if (step == null)
                return string.Empty;

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(step.GroupName))
                parts.Add(step.GroupName);

            if (!string.IsNullOrWhiteSpace(step.ApproverName))
                parts.Add("Onayci: " + step.ApproverName);

            if (!string.IsNullOrWhiteSpace(step.Message))
                parts.Add(step.Message);

            return string.Join(" | ", parts);
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

        private void SetLoading(bool value)
        {
            isLoading = value;

            if (txtSearch != null)
                txtSearch.IsEnabled = !value;

            if (treeUdp != null)
                treeUdp.IsEnabled = !value;

            UpdateBusyCursor();
        }

        private void UpdateBusyCursor()
        {
            Mouse.OverrideCursor = isLoading ? Cursors.Wait : null;
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
                string tableName = Safe(table.getoName(), "(adsiz tablo)");
                var properties = table.getoObjectProperty();

                if (properties != null)
                {
                    foreach (var property in properties)
                    {
                        if (!IsUdpProperty(property))
                            continue;

                        result.Rows.Add(CreateUdpRow(
                            Safe(table.getoObjectId(), string.Empty),
                            tableName,
                            Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                            Safe(property.getoPropertyValue(), string.Empty)));
                    }
                }
            }

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

        private static UdpRow CreateUdpRow(
            string tableObjectId,
            string tableName,
            string udpName,
            string value)
        {
            var row = new UdpRow
            {
                TableObjectId = tableObjectId,
                TableName = tableName,
                UdpName = udpName,
                DisplayUdpName = BuildDisplayUdpName(udpName),
                Value = value
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

        public sealed class UdpRow
        {
            public string TableObjectId { get; set; }

            public string TableName { get; set; }

            public string UdpName { get; set; }

            public string DisplayUdpName { get; set; }

            public string Value { get; set; }

            public string SearchText { get; set; }
        }

        public sealed class UdpDetailRow
        {
            public string Property { get; set; }

            public string Value { get; set; }
        }

        public sealed class ApprovalStepDisplayItem
        {
            public string StepText { get; set; }

            public string DetailText { get; set; }
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

        public string UdpNames { get; set; }

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
                ObjectName = objectName,
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

    }
}
