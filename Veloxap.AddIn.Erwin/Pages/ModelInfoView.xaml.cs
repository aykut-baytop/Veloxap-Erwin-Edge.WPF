using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Services;
using Veloxap.AddIn.Erwin.ViewModels;

namespace Veloxap.AddIn.Erwin.Pages
{
    public partial class ModelInfoView : UserControl
    {
        private const int SearchDelayMilliseconds = 500;
        private readonly ModelInfo tableUdpModelInfo;
        private readonly SCAPI.Application tableUdpApplication;
        private readonly SCAPI.PersistenceUnit tableUdpPersistenceUnit;
        private readonly RuleService catalogRuleService;
        private readonly string catalogName;
        private readonly string catalogLongId;
        private readonly DispatcherTimer searchTimer;
        private bool hasLoadedCatalogOverview;

        public ModelInfoView()
            : this(null, null, null, false, null, null, null)
        {
        }

        internal ModelInfoView(ModelInfo modelInfo)
            : this(modelInfo, null, null, false, null, null, null)
        {
        }

        internal ModelInfoView(
            ModelInfo modelInfo,
            SCAPI.Application application,
            SCAPI.PersistenceUnit persistenceUnit,
            bool showTableUdpTab)
            : this(modelInfo, application, persistenceUnit, showTableUdpTab, null, null, null)
        {
        }

        internal ModelInfoView(
            ModelInfo modelInfo,
            SCAPI.Application application,
            SCAPI.PersistenceUnit persistenceUnit,
            bool showTableUdpTab,
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            tableUdpModelInfo = modelInfo;
            tableUdpApplication = application;
            tableUdpPersistenceUnit = persistenceUnit;
            catalogRuleService = ruleService;
            this.catalogName = catalogName;
            this.catalogLongId = catalogLongId;

            searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds)
            };
            searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();
            Loaded += ModelInfoView_Loaded;
            tabModelSections.SelectionChanged += TabModelSections_SelectionChanged;

            DataContext = modelInfo == null
                ? new ModelInfoViewModel()
                : new ModelInfoViewModel(modelInfo);

            if (showTableUdpTab)
            {
                tabModelSections.SelectedItem = tabTableUdps;
                EnsureTableUdpViewLoaded();
            }
        }

        private async void ModelInfoView_Loaded(object sender, RoutedEventArgs e)
        {
            if (hasLoadedCatalogOverview)
                return;

            hasLoadedCatalogOverview = true;

            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            await viewModel.LoadCatalogOverviewAsync(
                catalogRuleService,
                catalogName,
                catalogLongId);
        }

        private void TabModelSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != tabModelSections)
                return;

            EnsureTableUdpViewLoaded();
        }

        private void EnsureTableUdpViewLoaded()
        {
            if (tabModelSections.SelectedItem != tabTableUdps ||
                tableUdpContent.Content != null)
            {
                return;
            }

            tableUdpContent.Content = new ModelUdpView(
                tableUdpModelInfo,
                tableUdpApplication,
                tableUdpPersistenceUnit,
                catalogRuleService,
                catalogName,
                catalogLongId);
        }

        private void ModelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            viewModel.SelectedModelItem = e.NewValue as ModelInfoViewModel.ModelDisplayItem;
        }

        private async void BtnUnlockCatalog_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            await viewModel.UnlockCatalogAsync(
                catalogRuleService,
                catalogName,
                catalogLongId);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
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

        private void ModelTreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null)
                return;

            var node = item.DataContext as ModelInfoViewModel.ModelDisplayItem;
            if (node == null || node.IsPlaceholder || !node.IsExpanded)
                return;

            node.EnsureChildrenLoaded();
        }

        private void ModelTreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = e.OriginalSource as TreeViewItem;
            if (item == null)
                return;

            var node = item.DataContext as ModelInfoViewModel.ModelDisplayItem;
            if (node == null || node.IsPlaceholder)
                return;

            node.EnsureChildrenLoaded();
        }

        private void ApplyFilter()
        {
            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            string filter = txtSearch == null
                ? string.Empty
                : txtSearch.Text;

            viewModel.ApplyFilter(filter);
        }
    }
}
