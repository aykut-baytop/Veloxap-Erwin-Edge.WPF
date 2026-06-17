using System.Windows;
using System.Windows.Controls;
using VeloxapEDGEWpfLib.Models;
using VeloxapEDGEWpfLib.ViewModels;

namespace VeloxapEDGEWpfLib.Pages
{
    public partial class ModelInfoView : UserControl
    {
        public ModelInfoView()
            : this(null)
        {
        }

        internal ModelInfoView(ModelInfo modelInfo)
        {
            InitializeComponent();

            DataContext = modelInfo == null
                ? new ModelInfoViewModel()
                : new ModelInfoViewModel(modelInfo);
        }

        private void ModelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ModelInfoViewModel viewModel)
            {
                viewModel.SelectedModelItem = e.NewValue as ModelInfoViewModel.ModelDisplayItem;
            }
        }
    }
}
