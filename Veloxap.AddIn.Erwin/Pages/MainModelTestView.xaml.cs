using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.ApplicationServices;
using System.Windows;
using System.Windows.Controls;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Services;
using Veloxap.AddIn.Erwin.ViewModels;
using VeloxapEDGErwinTools.AddIn;

namespace Veloxap.AddIn.Erwin.Pages
{
    public partial class MainModelTestView : UserControl
    {
        private readonly Window1 owner;
        private MainModelSelectionInfo selectedMainModelInfo;
        private bool isSubscribed;
        private int detectedChangeCount;
        private VeloxapEDGErwinLib veloxapEDGErwinLib;

        private readonly RuleService catalogRuleService;
        private string currentCatalogName;
        private string currentCatalogLongId;
        private string loadedCatalogOverviewKey;

        internal bool IsBoundToOwner
        {
            get { return owner != null; }
        }


        public MainModelTestView()
        {
            InitializeComponent();
            DataContext = new ModelInfoViewModel();
            SetSelectedMainModelInfo(MainModelSelectionInfo.Empty, false);
        }

        internal MainModelTestView(
            Window1 owner,
            MainModelSelectionInfo selectedMainModelInfo,
            ModelInfo modelInfo,
            SCAPI.Application oApp,
            RuleService ruleService, 
            string catalogName,
            string catalogLongId)
            : this()
        {
            this.owner = owner;
            veloxapEDGErwinLib = new VeloxapEDGErwinLib(ref oApp);
            catalogRuleService = ruleService;
            currentCatalogName = catalogName;
            currentCatalogLongId = catalogLongId;
            DataContext = modelInfo == null
                ? new ModelInfoViewModel()
                : new ModelInfoViewModel(modelInfo);

            SetSelectedMainModelInfo(selectedMainModelInfo, false);
            Loaded += MainModelTestView_Loaded;
            Unloaded += MainModelTestView_Unloaded;
        }

        private async void MainModelTestView_Loaded(object sender, RoutedEventArgs e)
        {
            if (owner != null && !isSubscribed)
            {
                owner.SelectedMainModelInfoChanged += Owner_SelectedMainModelInfoChanged;
                isSubscribed = true;
            }

            await LoadCatalogOverviewForCurrentModelAsync(false);
        }

        private void MainModelTestView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (owner == null || !isSubscribed)
                return;

            owner.SelectedMainModelInfoChanged -= Owner_SelectedMainModelInfoChanged;
            isSubscribed = false;
        }

        private async void Owner_SelectedMainModelInfoChanged(
            object sender,
            MainModelSelectionChangedEventArgs e)
        {
            SetSelectedMainModelInfo(e.SelectionInfo, true);
            await LoadCatalogOverviewForCurrentModelAsync(true);
        }

        private void SetSelectedMainModelInfo(
            MainModelSelectionInfo selectionInfo,
            bool countAsDetectedChange)
        {
            selectedMainModelInfo = selectionInfo ?? MainModelSelectionInfo.Empty;

            if (countAsDetectedChange)
                detectedChangeCount++;

            UpdateCatalogContext(selectionInfo);
            getTree(selectionInfo);
        }

        private void UpdateCatalogContext(MainModelSelectionInfo selectionInfo)
        {
            if (selectionInfo == null || !selectionInfo.HasSelection)
            {
                currentCatalogName = string.Empty;
                currentCatalogLongId = string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectionInfo.RuleModelName))
                currentCatalogName = selectionInfo.RuleModelName;

            if (!string.IsNullOrWhiteSpace(selectionInfo.RuleModelLongId))
                currentCatalogLongId = selectionInfo.RuleModelLongId;
        }

        private async Task LoadCatalogOverviewForCurrentModelAsync(bool forceReload)
        {
            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            string catalogOverviewKey =
                (currentCatalogName ?? string.Empty) +
                "|" +
                (currentCatalogLongId ?? string.Empty);

            if (!forceReload &&
                string.Equals(loadedCatalogOverviewKey, catalogOverviewKey, StringComparison.Ordinal))
            {
                return;
            }

            loadedCatalogOverviewKey = catalogOverviewKey;

            await viewModel.LoadCatalogOverviewAsync(
                catalogRuleService,
                currentCatalogName,
                currentCatalogLongId);
        }

        private void getTree(MainModelSelectionInfo selectionInfo)
        {
            try
            {
                ResetTreeAndDetails();

                if (selectionInfo == null || !selectionInfo.HasSelection)
                    return;

                if (veloxapEDGErwinLib == null || selectionInfo.SelectedIndex < 0)
                {
                    return;
                }

                List<(string, string, string)> modelObjectsList =
                    veloxapEDGErwinLib.getModelObjects(
                        selectionInfo.ObjectId,
                        selectionInfo.SelectedIndex) ??
                    new List<(string, string, string)>();

                string rootName = string.IsNullOrWhiteSpace(selectionInfo.DisplayName)
                    ? selectionInfo.RawName
                    : selectionInfo.DisplayName;

                var root = new ModelObjectTreeNode(
                    "Model",
                    rootName,
                    selectionInfo.ObjectId,
                    null,
                    true);

                root.IsExpanded = true;

                foreach (var modelObject in modelObjectsList)
                {
                    root.Children.Add(new ModelObjectTreeNode(
                        modelObject.Item1,
                        modelObject.Item2,
                        modelObject.Item3,
                        selectionInfo.ObjectId,
                        false));
                }

                treeModelObjects.ItemsSource = new List<ModelObjectTreeNode> { root };
                ShowNodeDetails(root);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TreeModelObjects_SelectedItemChanged(
            object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            ShowNodeDetails(e.NewValue as ModelObjectTreeNode);
        }

        private void ShowNodeDetails(ModelObjectTreeNode node)
        {
            if (node == null)
            {
                dgObjectDetails.ItemsSource = null;
                return;
            }


            List<ObjectPropertyDetail> details = LoadNodeDetails(node)
                            .Where(n =>
                    n.PropertyName.Trim() == ("Veri_Degeri") ||
                    n.PropertyName.Trim() == ("Banka_Gorece_Degeri") ||
                    n.PropertyName.Trim() == ("Guvenlik_Sinifi_Degeri")).ToList();


            ////n.PropertyName.Equals("Entity.Physical.Veri_Degeri") ||
            ////n.PropertyName.Equals("Entity.Physical.Is_Sureci")
            //).ToList();

            //foreach (ObjectPropertyDetail item in details)
            //{
            //    item.PropertyName = item.PropertyName.Split(',').LastOrDefault();
            //}

            //MessageBox.Show(details.FirstOrDefault().PropertyName + "  -  " + details.LastOrDefault().PropertyName);

            dgObjectDetails.ItemsSource = details;
        }

        private List<ObjectPropertyDetail> LoadNodeDetails(ModelObjectTreeNode node)
        {
            if (node == null ||
                selectedMainModelInfo == null ||
                selectedMainModelInfo.SelectedIndex < 0 ||
                veloxapEDGErwinLib == null)
            {
                return new List<ObjectPropertyDetail>();
            }

            var properties =
                veloxapEDGErwinLib.GetObjectProperties(node.IsRoot,node.ObjectId,node.ParentObjectId,selectedMainModelInfo.SelectedIndex);

            return properties.EntityProperties
                .Select(property => new ObjectPropertyDetail(
                    property.ClassName.Split('.').LastOrDefault(),
                    property.DataType,
                    property.Format,
                    property.Value))
                .ToList();
        }

        private void ResetTreeAndDetails()
        {
            treeModelObjects.ItemsSource = null;
            dgObjectDetails.ItemsSource = null;
        }

        private string BuildChangeStatusText(bool countAsDetectedChange)
        {
            if (!selectedMainModelInfo.HasSelection)
                return "Secim bekleniyor.";

            if (!countAsDetectedChange && detectedChangeCount == 0)
                return "Ilk secili ana model degiskeni ekrana yazildi. Change event bekleniyor.";

            return "Ana model change event'i algilandi. Son algilama: " +
                   DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private sealed class ModelObjectTreeNode
        {
            public ModelObjectTreeNode(
                string className,
                string name,
                string objectId,
                string parentObjectId,
                bool isRoot)
            {
                ClassName = className ?? string.Empty;
                Name = name ?? string.Empty;
                ObjectId = objectId ?? string.Empty;
                ParentObjectId = parentObjectId;
                IsRoot = isRoot;
                Children = new List<ModelObjectTreeNode>();
            }

            public string Header
            {
                get { return "(" + ClassName + ") " + Name; }
            }

            public string ClassName { get; private set; }

            public string Name { get; private set; }

            public string ObjectId { get; private set; }

            public string ParentObjectId { get; private set; }

            public bool IsRoot { get; private set; }

            public bool IsExpanded { get; set; }

            public List<ModelObjectTreeNode> Children { get; private set; }
        }

        private sealed class ObjectPropertyDetail
        {
            public ObjectPropertyDetail(
                string propertyName,
                string propertyType,
                string format,
                string value)
            {
                PropertyName = propertyName ?? string.Empty;
                PropertyType = propertyType ?? string.Empty;
                Format = format ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string PropertyName { get; set; }

            public string PropertyType { get; private set; }

            public string Format { get; private set; }

            public string Value { get; private set; }
        }

        private async void BtnDeleteCatalog_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ModelInfoViewModel;
            if (viewModel == null)
                return;

            await viewModel.DeleteCatalog(
                catalogRuleService,
                currentCatalogName,
                currentCatalogLongId);
        }
    }
}
