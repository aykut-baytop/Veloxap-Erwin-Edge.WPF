using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Veloxap.AddIn.Erwin.Models;

namespace Veloxap.AddIn.Erwin.ViewModels
{
    public class ModelInfoViewModel : INotifyPropertyChanged
    {
        private const int MinimumSearchLength = 3;
        private static readonly NaturalStringComparer NaturalComparer = new NaturalStringComparer();
        private readonly ModelInfo modelInfo;
        private ModelDisplayItem selectedModelItem;
        private string statusMessage;

        public ObservableCollection<ModelDisplayItem> ModelItems { get; }

        public ObservableCollection<PropertyDisplayItem> Properties { get; }

        public string StatusMessage
        {
            get
            {
                return statusMessage;
            }
            private set
            {
                if (statusMessage == value)
                    return;

                statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public ModelDisplayItem SelectedModelItem
        {
            get
            {
                return selectedModelItem;
            }
            set
            {
                if (selectedModelItem == value)
                    return;

                selectedModelItem = value;
                LoadProperties(selectedModelItem == null ? null : selectedModelItem.ObjectProperties);
                OnPropertyChanged(nameof(SelectedModelItem));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ModelInfoViewModel()
            : this(null)
        {
        }

        internal ModelInfoViewModel(ModelInfo modelInfo)
        {
            this.modelInfo = modelInfo;
            ModelItems = new ObservableCollection<ModelDisplayItem>();
            Properties = new ObservableCollection<PropertyDisplayItem>();
            StatusMessage = "Model bilgisi bulunamadi.";

            LoadModel();
        }

        public void ApplyFilter(string filter)
        {
            if (modelInfo == null)
            {
                ModelItems.Clear();
                SelectedModelItem = null;
                StatusMessage = "Model bilgisi bulunamadi.";
                return;
            }

            string normalizedFilter = (filter ?? string.Empty).Trim();
            bool hasShortSearch =
                normalizedFilter.Length > 0 &&
                normalizedFilter.Length < MinimumSearchLength;

            ModelItems.Clear();
            SelectedModelItem = null;

            if (normalizedFilter.Length >= MinimumSearchLength)
            {
                LoadSearchResults(normalizedFilter);
                return;
            }

            LoadModel();

            if (hasShortSearch)
            {
                StatusMessage =
                    "Arama icin en az " + MinimumSearchLength +
                    " karakter girin. Model agaci listeleniyor.";
            }
        }

        private void LoadModel()
        {
            ModelItems.Clear();

            if (modelInfo == null)
            {
                SelectedModelItem = null;
                StatusMessage = "Model bilgisi bulunamadi.";
                return;
            }

            var rootItem = CreateRootItem();
            rootItem.IsSelected = true;
            ModelItems.Add(rootItem);
            SelectedModelItem = rootItem;
            StatusMessage = "Model agaci listeleniyor.";
        }

        private void LoadSearchResults(string filter)
        {
            var rootItem = CreateRootSearchItem(filter);

            if (rootItem != null)
                ModelItems.Add(rootItem);

            int resultCount = CountNodes(ModelItems);

            if (resultCount == 0)
            {
                StatusMessage = "Arama kriterine uygun model bilgisi bulunamadi.";
                return;
            }

            ModelItems[0].IsSelected = true;
            SelectedModelItem = ModelItems[0];
            StatusMessage = resultCount + " model nesnesi bulundu.";
        }

        private ModelDisplayItem CreateRootItem()
        {
            return ModelDisplayItem.CreateGroup(
                "Model",
                modelInfo.getoName(),
                modelInfo.getoObjectId(),
                0,
                modelInfo.getoObjectProperty(),
                () => BuildChildItems(modelInfo.getoModelObject(), 1))
                .Expand();
        }

        private ModelDisplayItem CreateRootSearchItem(string filter)
        {
            List<ModelDisplayItem> children = BuildSearchChildItems(
                modelInfo.getoModelObject(),
                1,
                filter);

            bool selfMatches = Contains(BuildRootSearchText(), filter);
            if (!selfMatches && children.Count == 0)
                return null;

            var rootItem = ModelDisplayItem.CreateLeaf(
                "Model",
                modelInfo.getoName(),
                modelInfo.getoObjectId(),
                0,
                modelInfo.getoObjectProperty());

            foreach (var child in children)
                rootItem.Children.Add(child);

            return rootItem.Expand();
        }

        private static List<ModelDisplayItem> BuildChildItems(
            IEnumerable<ModelObject> modelObjects,
            int level)
        {
            var items = new List<ModelDisplayItem>();

            if (modelObjects == null)
                return items;

            foreach (var modelObject in SortModelObjects(modelObjects))
            {
                var item = BuildModelDisplayItem(modelObject, level);
                if (item != null)
                    items.Add(item);
            }

            return items;
        }

        private static ModelDisplayItem BuildModelDisplayItem(ModelObject modelObject, int level)
        {
            if (modelObject == null)
                return null;

            var childObjects = modelObject.getoModelObject();
            Func<List<ModelDisplayItem>> childFactory =
                childObjects == null || childObjects.Count == 0
                    ? null
                    : (Func<List<ModelDisplayItem>>)(() => BuildChildItems(childObjects, level + 1));

            return ModelDisplayItem.CreateGroup(
                modelObject.getoClassName(),
                modelObject.getoName(),
                modelObject.getoObjectId(),
                level,
                modelObject.getoObjectProperty(),
                childFactory);
        }

        private static List<ModelDisplayItem> BuildSearchChildItems(
            IEnumerable<ModelObject> modelObjects,
            int level,
            string filter)
        {
            var items = new List<ModelDisplayItem>();

            if (modelObjects == null)
                return items;

            foreach (var modelObject in SortModelObjects(modelObjects))
            {
                var item = BuildSearchDisplayItem(modelObject, level, filter);
                if (item != null)
                    items.Add(item);
            }

            return items;
        }

        private static ModelDisplayItem BuildSearchDisplayItem(
            ModelObject modelObject,
            int level,
            string filter)
        {
            if (modelObject == null)
                return null;

            List<ModelDisplayItem> children = BuildSearchChildItems(
                modelObject.getoModelObject(),
                level + 1,
                filter);

            bool selfMatches = Contains(BuildObjectSearchText(modelObject), filter);
            if (!selfMatches && children.Count == 0)
                return null;

            var item = ModelDisplayItem.CreateLeaf(
                modelObject.getoClassName(),
                modelObject.getoName(),
                modelObject.getoObjectId(),
                level,
                modelObject.getoObjectProperty());

            foreach (var child in children)
                item.Children.Add(child);

            return item.Expand();
        }

        private void LoadProperties(IEnumerable<ObjectProperty> objectProperties)
        {
            Properties.Clear();

            if (objectProperties == null)
                return;

            foreach (var objectProperty in SortProperties(objectProperties))
                Properties.Add(ToPropertyItem(objectProperty));
        }

        private static IEnumerable<ModelObject> SortModelObjects(IEnumerable<ModelObject> modelObjects)
        {
            return modelObjects
                .Where(modelObject => modelObject != null)
                .OrderBy(modelObject => Safe(modelObject.getoName(), string.Empty), NaturalComparer)
                .ThenBy(modelObject => Safe(modelObject.getoClassName(), string.Empty), NaturalComparer)
                .ThenBy(modelObject => Safe(modelObject.getoObjectId(), string.Empty), NaturalComparer);
        }

        private static IEnumerable<ObjectProperty> SortProperties(IEnumerable<ObjectProperty> objectProperties)
        {
            return objectProperties
                .Where(objectProperty => objectProperty != null)
                .OrderBy(objectProperty => Safe(objectProperty.getoPropertyClassName(), string.Empty), NaturalComparer)
                .ThenBy(objectProperty => Safe(objectProperty.getoPropertyType(), string.Empty), NaturalComparer)
                .ThenBy(objectProperty => Safe(objectProperty.getoPropertyValue(), string.Empty), NaturalComparer);
        }

        private static PropertyDisplayItem ToPropertyItem(ObjectProperty objectProperty)
        {
            return new PropertyDisplayItem
            {
                Property = objectProperty.getoPropertyClassName(),
                DataType = objectProperty.getoPropertyType(),
                Value = objectProperty.getoPropertyValue(),
                AsString = objectProperty.getoPropertyFormatAsString()
            };
        }

        private string BuildRootSearchText()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "Model",
                    modelInfo.getoName(),
                    modelInfo.getoObjectId(),
                    BuildPropertiesSearchText(modelInfo.getoObjectProperty())
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildObjectSearchText(ModelObject modelObject)
        {
            if (modelObject == null)
                return string.Empty;

            return string.Join(
                "\n",
                new[]
                {
                    modelObject.getoClassName(),
                    modelObject.getoName(),
                    modelObject.getoObjectId(),
                    BuildPropertiesSearchText(modelObject.getoObjectProperty())
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildPropertiesSearchText(IEnumerable<ObjectProperty> objectProperties)
        {
            if (objectProperties == null)
                return string.Empty;

            return string.Join(
                "\n",
                objectProperties
                    .Where(property => property != null)
                    .SelectMany(property => new[]
                    {
                        property.getoPropertyClassName(),
                        property.getoPropertyType(),
                        property.getoPropertyValue(),
                        property.getoPropertyFormatAsString()
                    })
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountNodes(IEnumerable<ModelDisplayItem> items)
        {
            if (items == null)
                return 0;

            int count = 0;

            foreach (var item in items)
            {
                if (item == null || item.IsPlaceholder)
                    continue;

                count++;
                count += CountNodes(item.Children);
            }

            return count;
        }

        private static string BuildDisplayName(string className, string name)
        {
            string safeClassName = string.IsNullOrWhiteSpace(className) ? "Object" : className;
            string safeName = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;

            return "(" + safeClassName + ") " + safeName;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public sealed class ModelDisplayItem : INotifyPropertyChanged
        {
            private Func<List<ModelDisplayItem>> childFactory;
            private bool childrenLoaded;
            private bool isExpanded;
            private bool isSelected;

            private ModelDisplayItem(
                string className,
                string objectName,
                string objectId,
                int level,
                IEnumerable<ObjectProperty> objectProperties)
            {
                ClassName = className;
                ObjectName = objectName;
                ObjectId = objectId;
                Level = level;
                Name = BuildDisplayName(className, objectName);
                ObjectProperties = objectProperties == null
                    ? new List<ObjectProperty>()
                    : objectProperties.ToList();
                Children = new ObservableCollection<ModelDisplayItem>();
                childrenLoaded = true;
            }

            private ModelDisplayItem(Func<List<ModelDisplayItem>> childFactory)
                : this(string.Empty, string.Empty, string.Empty, 0, null)
            {
                this.childFactory = childFactory;
                childrenLoaded = childFactory == null;

                if (!childrenLoaded)
                    Children.Add(CreatePlaceholder());
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public string Name { get; private set; }

            public string ClassName { get; private set; }

            public string ObjectName { get; private set; }

            public string ObjectId { get; private set; }

            public int Level { get; private set; }

            internal List<ObjectProperty> ObjectProperties { get; private set; }

            public ObservableCollection<ModelDisplayItem> Children { get; private set; }

            public bool IsPlaceholder { get; private set; }

            public bool IsExpanded
            {
                get
                {
                    return isExpanded;
                }
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
                get
                {
                    return isSelected;
                }
                set
                {
                    if (isSelected == value)
                        return;

                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }

            internal static ModelDisplayItem CreateGroup(
                string className,
                string objectName,
                string objectId,
                int level,
                IEnumerable<ObjectProperty> objectProperties,
                Func<List<ModelDisplayItem>> childFactory)
            {
                var item = new ModelDisplayItem(childFactory)
                {
                    ClassName = className,
                    ObjectName = objectName,
                    ObjectId = objectId,
                    Level = level,
                    Name = BuildDisplayName(className, objectName),
                    ObjectProperties = objectProperties == null
                        ? new List<ObjectProperty>()
                        : objectProperties.ToList()
                };

                return item;
            }

            internal static ModelDisplayItem CreateLeaf(
                string className,
                string objectName,
                string objectId,
                int level,
                IEnumerable<ObjectProperty> objectProperties)
            {
                return new ModelDisplayItem(
                    className,
                    objectName,
                    objectId,
                    level,
                    objectProperties);
            }

            public ModelDisplayItem Expand()
            {
                IsExpanded = true;
                EnsureChildrenLoaded();
                return this;
            }

            public void EnsureChildrenLoaded()
            {
                if (childrenLoaded)
                    return;

                childrenLoaded = true;
                Children.Clear();

                Func<List<ModelDisplayItem>> factory = childFactory;
                childFactory = null;

                if (factory == null)
                    return;

                foreach (var child in factory())
                    Children.Add(child);
            }

            private static ModelDisplayItem CreatePlaceholder()
            {
                return new ModelDisplayItem(string.Empty, "Yukleniyor...", string.Empty, 0, null)
                {
                    Name = "Yukleniyor...",
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

        public sealed class PropertyDisplayItem
        {
            public string Property { get; set; }

            public string DataType { get; set; }

            public string Value { get; set; }

            public string AsString { get; set; }
        }

        private sealed class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                if (x == null)
                    return -1;

                if (y == null)
                    return 1;

                int xIndex = 0;
                int yIndex = 0;

                while (xIndex < x.Length && yIndex < y.Length)
                {
                    char xChar = x[xIndex];
                    char yChar = y[yIndex];

                    if (char.IsDigit(xChar) && char.IsDigit(yChar))
                    {
                        int result = CompareNumberRuns(x, ref xIndex, y, ref yIndex);
                        if (result != 0)
                            return result;

                        continue;
                    }

                    int charResult = char.ToUpperInvariant(xChar)
                        .CompareTo(char.ToUpperInvariant(yChar));

                    if (charResult != 0)
                        return charResult;

                    xIndex++;
                    yIndex++;
                }

                return x.Length.CompareTo(y.Length);
            }

            private static int CompareNumberRuns(
                string x,
                ref int xIndex,
                string y,
                ref int yIndex)
            {
                int xStart = xIndex;
                int yStart = yIndex;

                while (xIndex < x.Length && char.IsDigit(x[xIndex]))
                    xIndex++;

                while (yIndex < y.Length && char.IsDigit(y[yIndex]))
                    yIndex++;

                string xNumber = TrimLeadingZeros(x.Substring(xStart, xIndex - xStart));
                string yNumber = TrimLeadingZeros(y.Substring(yStart, yIndex - yStart));

                int lengthResult = xNumber.Length.CompareTo(yNumber.Length);
                if (lengthResult != 0)
                    return lengthResult;

                int numberResult = string.CompareOrdinal(xNumber, yNumber);
                if (numberResult != 0)
                    return numberResult;

                return (xIndex - xStart).CompareTo(yIndex - yStart);
            }

            private static string TrimLeadingZeros(string value)
            {
                string trimmed = value.TrimStart('0');
                return trimmed.Length == 0 ? "0" : trimmed;
            }
        }
    }
}
