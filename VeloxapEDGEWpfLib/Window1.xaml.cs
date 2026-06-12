using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeloxapEDGErwinTools.AddIn;
using VeloxapEDGEWpfLib.Pages;

namespace VeloxapEDGEWpfLib
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
            rbModelInfo.Checked += Menu_Checked;
            rbValidation.Checked += Menu_Checked;
            rbRules.Checked += Menu_Checked;
            rbSettings.Checked += Menu_Checked;

            MainContent.Content = new ModelInfoView();
        }

        public void Init(ref SCAPI.Application app)
        {
            var veloxapEDGErwinLib = new VeloxapEDGErwinLib(ref app);
            models = new List<(string value, string key1, string key2)>();
            rules = new List<string>();
            isValidationActive = true;
            isValidationOk = false;
            PopulateModels();
        }

        private void Menu_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == rbModelInfo)
                MainContent.Content = new ModelInfoView();

            else if (sender == rbValidation)
                MainContent.Content = new ModelValidationView();

            else if (sender == rbRules)
                MainContent.Content = new ValidationRulesView();

            else if (sender == rbSettings)
                MainContent.Content = new SettingsView();
        }

        private void ModelInfo_Checked(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ModelInfoView();
        }

        //private void ModelValidation_Checked(object sender, RoutedEventArgs e)
        //{
        //    MainContent.Content = new ModelValidationView();
        //}

        //private void ValidationRules_Checked(object sender, RoutedEventArgs e)
        //{
        //    MainContent.Content = new ValidationRulesView();
        //}

        //private void Settings_Checked(object sender, RoutedEventArgs e)
        //{
        //    MainContent.Content = new SettingsView();
        //}

    }
}
