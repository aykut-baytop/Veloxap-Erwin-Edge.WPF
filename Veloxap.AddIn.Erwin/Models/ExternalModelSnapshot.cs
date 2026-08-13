using System;
using System.Collections.Generic;

namespace Veloxap.AddIn.Erwin.Models
{
    /// <summary>
    /// Serializable model data transferred from the in-process Erwin add-in to
    /// the isolated WPF UI host. No COM object crosses the process boundary.
    /// </summary>
    public sealed class ExternalModelSnapshot
    {
        public List<ExternalModelSnapshotItem> Models { get; set; }

        public ExternalModelSnapshot()
        {
            Models = new List<ExternalModelSnapshotItem>();
        }
    }

    public sealed class ExternalModelSnapshotItem
    {
        public string DisplayName { get; set; }
        public string ObjectId { get; set; }
        public string PersistenceObjectId { get; set; }
        public ModelSnapshotInfo Model { get; set; }
        public List<ExternalModelObjectSnapshot> ModelObjects { get; set; }

        public ExternalModelSnapshotItem()
        {
            ModelObjects = new List<ExternalModelObjectSnapshot>();
        }
    }

    /// <summary>
    /// Exact input/output of MainModelTestView's original SCAPI calls.
    /// </summary>
    public sealed class ExternalModelObjectSnapshot
    {
        public string ClassName { get; set; }
        public string Name { get; set; }
        public string ObjectId { get; set; }
        public string ParentObjectId { get; set; }
        public bool IsRoot { get; set; }
        public ObjectPropertiesResult Properties { get; set; }
    }

    public sealed class ModelSnapshotInfo
    {
        public string Name { get; set; }
        public string ObjectId { get; set; }
        public string Location { get; set; }
        public List<ModelSnapshotObject> Objects { get; set; }
        public List<ModelSnapshotProperty> Properties { get; set; }

        public ModelSnapshotInfo()
        {
            Objects = new List<ModelSnapshotObject>();
            Properties = new List<ModelSnapshotProperty>();
        }

        internal static ModelSnapshotInfo FromModelInfo(ModelInfo model)
        {
            var snapshot = new ModelSnapshotInfo();
            if (model == null)
                return snapshot;

            snapshot.Name = model.getoName();
            snapshot.ObjectId = model.getoObjectId();
            snapshot.Location = model.getoLocation();
            snapshot.Properties = ModelSnapshotProperty.FromProperties(model.getoObjectProperty());
            snapshot.Objects = ModelSnapshotObject.FromObjects(model.getoModelObject());
            return snapshot;
        }

        internal ModelInfo ToModelInfo()
        {
            var model = new ModelInfo();
            model.setoName(Name);
            model.setoObjectId(ObjectId);
            model.setoLocation(Location);
            model.setoObjectProperty(ModelSnapshotProperty.ToProperties(Properties));
            model.setoModelObject(ModelSnapshotObject.ToObjects(Objects));
            return model;
        }
    }

    public sealed class ModelSnapshotObject
    {
        public string ObjectId { get; set; }
        public string ClassName { get; set; }
        public string Name { get; set; }
        public List<ModelSnapshotProperty> Properties { get; set; }
        public List<ModelSnapshotObject> Children { get; set; }

        public ModelSnapshotObject()
        {
            Properties = new List<ModelSnapshotProperty>();
            Children = new List<ModelSnapshotObject>();
        }

        internal static List<ModelSnapshotObject> FromObjects(List<ModelObject> objects)
        {
            var snapshots = new List<ModelSnapshotObject>();
            foreach (var item in objects ?? new List<ModelObject>())
            {
                if (item == null)
                    continue;

                snapshots.Add(new ModelSnapshotObject
                {
                    ObjectId = item.getoObjectId(),
                    ClassName = item.getoClassName(),
                    Name = item.getoName(),
                    Properties = ModelSnapshotProperty.FromProperties(item.getoObjectProperty()),
                    Children = FromObjects(item.getoModelObject())
                });
            }

            return snapshots;
        }

        internal static List<ModelObject> ToObjects(List<ModelSnapshotObject> snapshots)
        {
            var objects = new List<ModelObject>();
            foreach (var item in snapshots ?? new List<ModelSnapshotObject>())
            {
                if (item == null)
                    continue;

                var modelObject = new ModelObject();
                modelObject.setModelObject(item.ObjectId, item.ClassName, item.Name);
                modelObject.setoObjectProperty(ModelSnapshotProperty.ToProperties(item.Properties));
                modelObject.setoModelObjects(ToObjects(item.Children));
                objects.Add(modelObject);
            }

            return objects;
        }
    }

    public sealed class ModelSnapshotProperty
    {
        public string ClassId { get; set; }
        public string ClassName { get; set; }
        public string Type { get; set; }
        public string Format { get; set; }
        public string Value { get; set; }
        public string LeftValue { get; set; }
        public string RightValue { get; set; }

        internal static List<ModelSnapshotProperty> FromProperties(List<ObjectProperty> properties)
        {
            var snapshots = new List<ModelSnapshotProperty>();
            foreach (var item in properties ?? new List<ObjectProperty>())
            {
                if (item == null)
                    continue;

                snapshots.Add(new ModelSnapshotProperty
                {
                    ClassId = item.getoPropertyClassID(),
                    ClassName = item.getoPropertyClassName(),
                    Type = item.getoPropertyType(),
                    Format = item.getoPropertyFormatAsString(),
                    Value = item.getoPropertyValue(),
                    LeftValue = item.getLeftValue(),
                    RightValue = item.getRightValue()
                });
            }

            return snapshots;
        }

        internal static List<ObjectProperty> ToProperties(List<ModelSnapshotProperty> snapshots)
        {
            var properties = new List<ObjectProperty>();
            foreach (var item in snapshots ?? new List<ModelSnapshotProperty>())
            {
                if (item == null)
                    continue;

                var property = new ObjectProperty();
                property.setObjectProperty(item.ClassId, item.ClassName, item.Type, item.Format, item.Value);
                property.setLeftValue(item.LeftValue);
                property.setRightValue(item.RightValue);
                properties.Add(property);
            }

            return properties;
        }
    }
}
