using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Veloxap.AddIn.Erwin.Models;

namespace Veloxap.AddIn.Erwin.ViewModels
{
    public class ModelInfoViewModel : INotifyPropertyChanged
    {
        private ModelDisplayItem selectedModelItem;

        public ObservableCollection<ModelDisplayItem> ModelItems { get; }

        public ObservableCollection<PropertyDisplayItem> Properties { get; }

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
                LoadProperties(selectedModelItem?.ObjectProperties);
                OnPropertyChanged(nameof(SelectedModelItem));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ModelInfoViewModel()
        {
            ModelItems = new ObservableCollection<ModelDisplayItem>();
            Properties = new ObservableCollection<PropertyDisplayItem>();
        }

        internal ModelInfoViewModel(ModelInfo modelInfo)
            : this()
        {
            LoadModel(modelInfo);
        }

        private void LoadModel(ModelInfo modelInfo)
        {
            if (modelInfo == null)
                return;

            var rootItem = new ModelDisplayItem
            {
                Name = BuildDisplayName("Model", modelInfo.getoName(), 0),
                ObjectProperties = modelInfo.getoObjectProperty() ?? new List<ObjectProperty>()
            };

            var modelObjects = modelInfo.getoModelObject();
            if (modelObjects != null)
            {
                foreach (var modelObject in modelObjects)
                    rootItem.Children.Add(BuildModelDisplayItem(modelObject, 1));
            }

            ModelItems.Add(rootItem);

            if (ModelItems.Count > 0)
                SelectedModelItem = ModelItems[0];
        }

        private static ModelDisplayItem BuildModelDisplayItem(ModelObject modelObject, int level)
        {
            if (modelObject == null)
                return null;

            var node = new ModelDisplayItem
            {
                Name = BuildDisplayName(modelObject.getoClassName(), modelObject.getoName(), level),
                ObjectProperties = modelObject.getoObjectProperty() ?? new List<ObjectProperty>()
            };

            var childObjects = modelObject.getoModelObject();
            if (childObjects != null)
            {
                foreach (var childObject in childObjects)
                {
                    var childNode = BuildModelDisplayItem(childObject, level + 1);
                    if (childNode != null)
                        node.Children.Add(childNode);
                }
            }

            node.IsExpanded = node.Children.Count <= 1;
            return node;
        }

        private void LoadProperties(IEnumerable<ObjectProperty> objectProperties)
        {
            Properties.Clear();

            if (objectProperties == null)
                return;

            foreach (var objectProperty in objectProperties)
                Properties.Add(ToPropertyItem(objectProperty));
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

        private static string BuildDisplayName(string className, string name, int level)
        {
            string prefix = level <= 0 ? string.Empty : new string(' ', level * 2);
            string safeClassName = string.IsNullOrWhiteSpace(className) ? "Object" : className;
            string safeName = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;

            return $"{prefix}({safeClassName}) {safeName}";
        }

        public sealed class ModelDisplayItem
        {
            public string Name { get; set; }

            internal List<ObjectProperty> ObjectProperties { get; set; } = new List<ObjectProperty>();

            public ObservableCollection<ModelDisplayItem> Children { get; } = new ObservableCollection<ModelDisplayItem>();

            public bool IsExpanded { get; set; }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public sealed class PropertyDisplayItem
        {
            public string Property { get; set; }

            public string DataType { get; set; }

            public string Value { get; set; }

            public string AsString { get; set; }
        }
    }
}
