using System.Windows.Controls;
using VeloxapEDGEWpfLib.ViewModels;

namespace VeloxapEDGEWpfLib.Pages
{
    public partial class ModelInfoView : UserControl
    {
        public ModelInfoView()
        {
            InitializeComponent();

            DataContext = new ModelInfoViewModel();
        }
    }
}