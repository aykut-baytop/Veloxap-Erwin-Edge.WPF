using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.ViewModels;

namespace Veloxap.AddIn.Erwin.Pages
{
    public partial class ModelInfoView : UserControl
    {
        private const int SearchDelayMilliseconds = 500;
        private readonly DispatcherTimer searchTimer;

        public ModelInfoView()
            : this(null)
        {
        }

        internal ModelInfoView(ModelInfo modelInfo)
        {
            searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds)
            };
            searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();

            DataContext = modelInfo == null
                ? new ModelInfoViewModel()
                : new ModelInfoViewModel(modelInfo);
        }

        private void ModelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            viewModel.SelectedModelItem = e.NewValue as ModelInfoViewModel.ModelDisplayItem;
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
