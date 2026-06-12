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
using VeloxapEDGEWpfLib.Models;
using VeloxapEDGEWpfLib.Pages;

namespace VeloxapEDGEWpfLib
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        private VeloxapEDGErwinLib veloxapEDGErwinLib;
        private List<string> rules;
        private List<(string value, string key1, string key2)> models;

        private string selectedModelLongId;
        private string selectedModelName;
        private string selectedModelVersionNo;
        private List<(int key, string val)> selectedModelAllVersions;
        private ModelInfo currentModelInfo;

        private bool isValidationActive;
        private bool isValidationOk;

        public Window1()
        {
            InitializeComponent();
            rbModelInfo.Checked += Menu_Checked;
            rbValidation.Checked += Menu_Checked;
            rbRules.Checked += Menu_Checked;
            rbSettings.Checked += Menu_Checked;
            cmbMainModel.SelectionChanged += CmbMainModel_SelectionChanged;

            MainContent.Content = new ModelInfoView();
        }

        public void Init(ref SCAPI.Application app)
        {
            veloxapEDGErwinLib = new VeloxapEDGErwinLib(ref app);
            models = new List<(string value, string key1, string key2)>();
            rules = new List<string>();
            isValidationActive = true;
            isValidationOk = false;
            PopulateModels();
        }

        private void Menu_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == rbModelInfo)
                MainContent.Content = new ModelInfoView(currentModelInfo);

            else if (sender == rbValidation)
                MainContent.Content = new ModelValidationView();

            else if (sender == rbRules)
                MainContent.Content = new ValidationRulesView();

            else if (sender == rbSettings)
                MainContent.Content = new SettingsView();
        }

        private void ModelInfo_Checked(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ModelInfoView(currentModelInfo);
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


        public void PopulateModels()
        {
            models = veloxapEDGErwinLib.getModelsNamePath() ?? new List<(string value, string key1, string key2)>();

            var modelItems = models
                .Select(model => new ModelSelection(model.value, model.key1, model.key2))
                .ToList();

            cmbMainModel.DisplayMemberPath = nameof(ModelSelection.Name);
            cmbMainModel.ItemsSource = modelItems;

            if (modelItems.Count > 0)
                cmbMainModel.SelectedIndex = 0;
            else
                MainContent.Content = new ModelInfoView();
            //comboBox1.Items.Clear();
            //models = veloxapEDGErwinLib.getModelsNamePath();



            //comboBox1.DataSource = models;
            //comboBox1.DisplayMember = "value";




            //if (comboBox1.Items.Count > 0)
            //    comboBox1.SelectedIndex = 0;

        }

        private void CmbMainModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedModel = cmbMainModel.SelectedItem as ModelSelection;
            if (veloxapEDGErwinLib == null || selectedModel == null)
                return;

            selectedModelName = selectedModel.Name;
            selectedModelLongId = selectedModel.ObjectId;

            currentModelInfo = veloxapEDGErwinLib.loadModelObject(
                selectedModel.ObjectId,
                selectedModel.PersistenceObjectId);

            if (rbModelInfo.IsChecked == true)
                MainContent.Content = new ModelInfoView(currentModelInfo);
        }

        private sealed class ModelSelection
        {
            public ModelSelection(string name, string objectId, string persistenceObjectId)
            {
                Name = name;
                ObjectId = objectId;
                PersistenceObjectId = persistenceObjectId;
            }

            public string Name { get; }

            public string ObjectId { get; }

            public string PersistenceObjectId { get; }
        }

    }
}
