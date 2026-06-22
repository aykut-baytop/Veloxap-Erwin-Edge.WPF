using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VeloxapEDGErwinTools.AddIn;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Models.Integrate;
using Veloxap.AddIn.Erwin.Services;

namespace Veloxap.AddIn.Erwin.Pages
{
    public partial class ModelComparisonView : UserControl
    {
        private readonly SCAPI.Application application;
        private readonly VeloxapEDGErwinLib erwinLib;
        private readonly List<(string value, string key1, string key2)> models;

        private ModelDiffResult diff;
        private int leftModelIndex;
        private int rightModelIndex;
        private string leftRole;
        private string rightRole;
        private bool isBusy;

        public ModelComparisonView()
            : this(null, null, null)
        {
        }

        internal ModelComparisonView(
            VeloxapEDGErwinLib erwinLib,
            SCAPI.Application application,
            List<(string value, string key1, string key2)> models)
        {
            InitializeComponent();

            this.erwinLib = erwinLib;
            this.application = application;
            this.models = models == null
                ? new List<(string value, string key1, string key2)>()
                : models.ToList();

            leftRole = "DEV";
            rightRole = "INT";
            leftModelIndex = 0;
            rightModelIndex = 1;

            ResolveModelSides();
            UpdateModelLabels();
            ClearDiffTrees();
            SetStatus("HazÄ±r", false);
        }

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            CompareModels(true);
        }

        private void BtnApplyRightToLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyDiff(ApplyDirection.RightToLeft);
        }

        private void BtnApplyLeftToRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyDiff(ApplyDirection.LeftToRight);
        }

        private bool CompareModels(bool showMessages)
        {
            if (!ValidateModelCount(showMessages))
                return false;

            SetBusy(true);
            SetStatus("Modeller okunuyor...", false);

            try
            {
                ResolveModelSides();
                UpdateModelLabels();

                ModelInfo leftModel = erwinLib.loadModelObjectForIntegrate(leftModelIndex);
                ModelInfo rightModel = erwinLib.loadModelObjectForIntegrate(rightModelIndex);

                if (leftModel == null || rightModel == null)
                    throw new InvalidOperationException("Modeller okunamadÄ±.");

                diff = ModelObjectComparer.Compare(leftModel, rightModel);
                LoadDiffTrees();

                SetStatus("KarÅŸÄ±laÅŸtÄ±rma tamamlandÄ±.", false);
                return true;
            }
            catch (Exception ex)
            {
                diff = null;
                ClearDiffTrees();
                SetStatus("KarÅŸÄ±laÅŸtÄ±rma hatasÄ±: " + ex.Message, true);

                if (showMessages)
                {
                    MessageBox.Show(
                        "Model karÅŸÄ±laÅŸtÄ±rma iÅŸlemi tamamlanamadÄ±.\n\n" + ex.Message,
                        "Model KarÅŸÄ±laÅŸtÄ±rma",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return false;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplyDiff(ApplyDirection direction)
        {
            if (diff == null)
            {
                MessageBox.Show(
                    "Ã–nce modelleri karÅŸÄ±laÅŸtÄ±rÄ±n.",
                    "Model AktarÄ±mÄ±",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            List<ModelObject> objects = direction == ApplyDirection.RightToLeft
                ? diff.OnlyInRight
                : diff.OnlyInLeft;

            if (objects == null || objects.Count == 0)
            {
                SetStatus("Bu yÃ¶nde aktarÄ±lacak fark yok.", false);
                return;
            }

            int targetModelIndex = direction == ApplyDirection.RightToLeft
                ? leftModelIndex
                : rightModelIndex;

            SCAPI.PersistenceUnit targetPersistenceUnit =
                erwinLib == null
                    ? null
                    : erwinLib.getPersistenceUnit(targetModelIndex);

            if (targetPersistenceUnit == null || application == null)
            {
                SetStatus("AktarÄ±m iÃ§in erwin oturumu hazÄ±r deÄŸil.", true);
                return;
            }

            string directionLabel = direction == ApplyDirection.RightToLeft
                ? rightRole + " -> " + leftRole
                : leftRole + " -> " + rightRole;

            MessageBoxResult confirmation = MessageBox.Show(
                directionLabel + " aktarÄ±mÄ± baÅŸlatÄ±lsÄ±n mÄ±?",
                "Model AktarÄ±mÄ±",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            SetBusy(true);
            SetStatus(directionLabel + " aktarÄ±mÄ± yapÄ±lÄ±yor...", false);

            try
            {
                var applyService = new ModelDiffApplyService(
                    application,
                    targetPersistenceUnit);

                ModelDiffApplyResult applyResult = applyService.Apply(diff, direction);

                if (applyResult.AppliedChanges == 0)
                {
                    string detail = BuildApplyResultMessage(applyResult);
                    SetStatus("AktarÄ±mda uygulanabilir deÄŸiÅŸiklik bulunamadÄ±. " + applyResult.ToSummary(), true);

                    MessageBox.Show(
                        "AktarÄ±m komutu Ã§alÄ±ÅŸtÄ± ancak modele uygulanmÄ±ÅŸ bir deÄŸiÅŸiklik tespit edilemedi.\n\n" + detail,
                        "Model AktarÄ±mÄ±",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                SetStatus("AktarÄ±m tamamlandÄ±. Farklar yenileniyor...", false);
                CompareModels(false);

                MessageBox.Show(
                    "AktarÄ±m tamamlandÄ±.\n\n" + applyResult.ToSummary(),
                    "Model AktarÄ±mÄ±",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("AktarÄ±m hatasÄ±: " + ex.Message, true);

                MessageBox.Show(
                    "AktarÄ±m tamamlanamadÄ±.\n\n" + ex.Message,
                    "Model AktarÄ±mÄ±",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private bool ValidateModelCount(bool showMessages)
        {
            int modelCount = erwinLib == null
                ? 0
                : erwinLib.getNumberOfModels();

            if (modelCount == 2)
                return true;

            SetStatus("KarÅŸÄ±laÅŸtÄ±rma iÃ§in tam olarak 2 aÃ§Ä±k model gerekir.", true);

            if (showMessages)
            {
                MessageBox.Show(
                    "KarÅŸÄ±laÅŸtÄ±rma iÃ§in erwin iÃ§inde tam olarak 2 model aÃ§Ä±k olmalÄ±dÄ±r.",
                    "Model KarÅŸÄ±laÅŸtÄ±rma",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return false;
        }

        private void ResolveModelSides()
        {
            leftModelIndex = 0;
            rightModelIndex = 1;
            leftRole = "DEV";
            rightRole = "INT";

            if (models.Count < 2)
                return;

            if (ContainsIntegrationMarker(models[0].value))
            {
                leftModelIndex = 1;
                rightModelIndex = 0;
            }
        }

        private void UpdateModelLabels()
        {
            string leftName = GetModelDisplayName(leftModelIndex);
            string rightName = GetModelDisplayName(rightModelIndex);

            txtModelPair.Text = leftRole + ": " + leftName + " / " + rightRole + ": " + rightName;
            txtLeftModelName.Text = leftRole + ": " + leftName;
            txtRightModelName.Text = rightRole + ": " + rightName;

            txtRightToLeftTitle.Text = rightRole + " -> " + leftRole;
            txtRightToLeftSubtitle.Text = leftRole + " tarafÄ±na uygulanacak farklar";
            txtApplyRightToLeftButton.Text = rightRole + " -> " + leftRole + " Aktar";

            txtLeftToRightTitle.Text = leftRole + " -> " + rightRole;
            txtLeftToRightSubtitle.Text = rightRole + " tarafÄ±na uygulanacak farklar";
            txtApplyLeftToRightButton.Text = leftRole + " -> " + rightRole + " Aktar";
        }

        private void LoadDiffTrees()
        {
            var rightToLeftNodes = BuildTreeItems(
                diff == null ? null : diff.OnlyInRight,
                rightRole,
                leftRole);

            var leftToRightNodes = BuildTreeItems(
                diff == null ? null : diff.OnlyInLeft,
                leftRole,
                rightRole);

            treeRightToLeft.ItemsSource = rightToLeftNodes;
            treeLeftToRight.ItemsSource = leftToRightNodes;

            int rightToLeftObjects = CountObjects(diff == null ? null : diff.OnlyInRight);
            int rightToLeftProperties = CountProperties(diff == null ? null : diff.OnlyInRight);
            int leftToRightObjects = CountObjects(diff == null ? null : diff.OnlyInLeft);
            int leftToRightProperties = CountProperties(diff == null ? null : diff.OnlyInLeft);

            txtRightToLeftCount.Text = FormatCount(rightToLeftObjects, rightToLeftProperties);
            txtLeftToRightCount.Text = FormatCount(leftToRightObjects, leftToRightProperties);
            txtTotalDiffCount.Text = (rightToLeftObjects + rightToLeftProperties + leftToRightObjects + leftToRightProperties).ToString();

            emptyRightToLeft.Visibility = rightToLeftNodes.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            emptyLeftToRight.Visibility = leftToRightNodes.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateApplyButtons();
        }

        private void ClearDiffTrees()
        {
            treeRightToLeft.ItemsSource = new ObservableCollection<DiffTreeItem>();
            treeLeftToRight.ItemsSource = new ObservableCollection<DiffTreeItem>();
            txtRightToLeftCount.Text = "0";
            txtLeftToRightCount.Text = "0";
            txtTotalDiffCount.Text = "0";
            emptyRightToLeft.Visibility = Visibility.Visible;
            emptyLeftToRight.Visibility = Visibility.Visible;
            UpdateApplyButtons();
        }

        private ObservableCollection<DiffTreeItem> BuildTreeItems(
            List<ModelObject> objects,
            string sourceRole,
            string targetRole)
        {
            var items = new ObservableCollection<DiffTreeItem>();

            if (objects == null)
                return items;

            foreach (var obj in objects)
                items.Add(CreateObjectItem(obj, sourceRole, targetRole));

            return items;
        }

        private DiffTreeItem CreateObjectItem(
            ModelObject obj,
            string sourceRole,
            string targetRole)
        {
            string className = SafeText(obj.getoClassName(), "Object");
            string name = SafeText(obj.getoName(), "(adsÄ±z)");
            int propertyCount = obj.getoObjectProperty() == null ? 0 : obj.getoObjectProperty().Count;
            int childCount = obj.getoModelObject() == null ? 0 : obj.getoModelObject().Count;

            var item = DiffTreeItem.CreateObject(
                className + " : " + name,
                BuildObjectDescription(propertyCount, childCount),
                className);

            if (obj.getoObjectProperty() != null)
            {
                foreach (var property in obj.getoObjectProperty())
                    item.Children.Add(CreatePropertyItem(property, sourceRole, targetRole));
            }

            if (obj.getoModelObject() != null)
            {
                foreach (var child in obj.getoModelObject())
                    item.Children.Add(CreateObjectItem(child, sourceRole, targetRole));
            }

            return item;
        }

        private DiffTreeItem CreatePropertyItem(
            ObjectProperty property,
            string sourceRole,
            string targetRole)
        {
            string propertyName = SafeText(property.getoPropertyClassName(), "Property");
            string propertyType = SafeText(property.getoPropertyType(), string.Empty);
            string leftValue = SafeValue(property.getLeftValue());
            string rightValue = SafeValue(property.getRightValue());

            var item = DiffTreeItem.CreateProperty(
                propertyName,
                string.IsNullOrWhiteSpace(propertyType)
                    ? "Property farkÄ±"
                    : "Property farkÄ± / " + propertyType);

            item.Children.Add(DiffTreeItem.CreateValue(leftRole + ": " + leftValue));
            item.Children.Add(DiffTreeItem.CreateValue(rightRole + ": " + rightValue));

            return item;
        }

        private void SetBusy(bool value)
        {
            isBusy = value;
            btnCompare.IsEnabled = !isBusy;
            Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
            UpdateApplyButtons();
        }

        private void UpdateApplyButtons()
        {
            bool hasRightToLeft = diff != null &&
                                  diff.OnlyInRight != null &&
                                  diff.OnlyInRight.Count > 0;

            bool hasLeftToRight = diff != null &&
                                  diff.OnlyInLeft != null &&
                                  diff.OnlyInLeft.Count > 0;

            btnApplyRightToLeft.IsEnabled = !isBusy && hasRightToLeft;
            btnApplyLeftToRight.IsEnabled = !isBusy && hasLeftToRight;
        }

        private void SetStatus(string message, bool isError)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
                : new SolidColorBrush(Color.FromRgb(55, 65, 81));
        }

        private string GetModelDisplayName(int index)
        {
            if (index < 0 || index >= models.Count)
                return "-";

            return string.IsNullOrWhiteSpace(models[index].value)
                ? "-"
                : models[index].value;
        }

        private static bool ContainsIntegrationMarker(string modelName)
        {
            return !string.IsNullOrWhiteSpace(modelName) &&
                   modelName.IndexOf("Int", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildObjectDescription(int propertyCount, int childCount)
        {
            var parts = new List<string>();

            if (propertyCount > 0)
                parts.Add(propertyCount + " property");

            if (childCount > 0)
                parts.Add(childCount + " alt nesne");

            return parts.Count == 0
                ? string.Empty
                : string.Join(" / ", parts);
        }

        private static string FormatCount(int objectCount, int propertyCount)
        {
            return objectCount + " nesne / " + propertyCount + " property";
        }

        private static int CountObjects(List<ModelObject> objects)
        {
            if (objects == null)
                return 0;

            int count = 0;

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                count++;
                count += CountObjects(obj.getoModelObject());
            }

            return count;
        }

        private static int CountProperties(List<ModelObject> objects)
        {
            if (objects == null)
                return 0;

            int count = 0;

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                if (obj.getoObjectProperty() != null)
                    count += obj.getoObjectProperty().Count;

                count += CountProperties(obj.getoModelObject());
            }

            return count;
        }

        private static string SafeValue(string value)
        {
            return value == null
                ? "<YOK>"
                : value;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
        }

        private static string BuildApplyResultMessage(ModelDiffApplyResult result)
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

        public sealed class DiffTreeItem
        {
            public DiffTreeItem()
            {
                Children = new ObservableCollection<DiffTreeItem>();
                TitleWeight = FontWeights.Normal;
                TitleBrush = new SolidColorBrush(Color.FromRgb(17, 24, 39));
                BadgeVisibility = Visibility.Collapsed;
                DescriptionVisibility = Visibility.Collapsed;
            }

            public string Title { get; set; }

            public string Description { get; set; }

            public Visibility DescriptionVisibility { get; set; }

            public string Icon { get; set; }

            public Brush IconBackground { get; set; }

            public Brush IconBrush { get; set; }

            public string BadgeText { get; set; }

            public Visibility BadgeVisibility { get; set; }

            public Brush BadgeBackground { get; set; }

            public Brush BadgeBrush { get; set; }

            public FontWeight TitleWeight { get; set; }

            public Brush TitleBrush { get; set; }

            public ObservableCollection<DiffTreeItem> Children { get; private set; }

            public static DiffTreeItem CreateObject(
                string title,
                string description,
                string className)
            {
                bool isEntity = string.Equals(
                    className,
                    "Entity",
                    StringComparison.OrdinalIgnoreCase);

                return new DiffTreeItem
                {
                    Title = title,
                    Description = description,
                    DescriptionVisibility = string.IsNullOrWhiteSpace(description)
                        ? Visibility.Collapsed
                        : Visibility.Visible,
                    Icon = isEntity ? "\uE8A5" : "\uE8EC",
                    IconBackground = new SolidColorBrush(isEntity
                        ? Color.FromRgb(224, 242, 254)
                        : Color.FromRgb(237, 233, 254)),
                    IconBrush = new SolidColorBrush(isEntity
                        ? Color.FromRgb(3, 105, 161)
                        : Color.FromRgb(109, 40, 217)),
                    BadgeText = className,
                    BadgeVisibility = Visibility.Visible,
                    BadgeBackground = new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                    BadgeBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                    TitleWeight = FontWeights.SemiBold
                };
            }

            public static DiffTreeItem CreateProperty(
                string title,
                string description)
            {
                return new DiffTreeItem
                {
                    Title = title,
                    Description = description,
                    DescriptionVisibility = Visibility.Visible,
                    Icon = "\uE713",
                    IconBackground = new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                    IconBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                    BadgeText = "Property",
                    BadgeVisibility = Visibility.Visible,
                    BadgeBackground = new SolidColorBrush(Color.FromRgb(254, 242, 242)),
                    BadgeBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27)),
                    TitleWeight = FontWeights.SemiBold,
                    TitleBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27))
                };
            }

            public static DiffTreeItem CreateValue(string title)
            {
                return new DiffTreeItem
                {
                    Title = title,
                    Icon = "\uE8D2",
                    IconBackground = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                    IconBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    TitleBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81))
                };
            }
        }
    }
}
