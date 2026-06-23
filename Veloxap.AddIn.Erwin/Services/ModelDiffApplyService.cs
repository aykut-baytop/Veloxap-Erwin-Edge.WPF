using System;
using System.Collections.Generic;
using System.Linq;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Models.Integrate;

namespace Veloxap.AddIn.Erwin.Services
{
    internal enum ApplyDirection
    {
        LeftToRight,
        RightToLeft
    }

    internal class ModelDiffApplyService
    {
        private readonly SCAPI.Application application;
        private readonly SCAPI.PersistenceUnit targetPersistenceUnit;

        public ModelDiffApplyService(
            SCAPI.Application application,
            SCAPI.PersistenceUnit targetPersistenceUnit)
        {
            this.application = application;
            this.targetPersistenceUnit = targetPersistenceUnit;
        }

        public ModelDiffApplyResult ApplyLeftToRight(ModelDiffResult diff)
        {
            if (diff == null)
                return new ModelDiffApplyResult();

            return ApplyObjects(
                diff.OnlyInLeft,
                ApplyDirection.LeftToRight);
        }

        public ModelDiffApplyResult ApplyRightToLeft(ModelDiffResult diff)
        {
            if (diff == null)
                return new ModelDiffApplyResult();

            return ApplyObjects(
                diff.OnlyInRight,
                ApplyDirection.RightToLeft);
        }

        public ModelDiffApplyResult Apply(
            ModelDiffResult diff,
            ApplyDirection direction)
        {
            if (direction == ApplyDirection.LeftToRight)
                return ApplyLeftToRight(diff);

            return ApplyRightToLeft(diff);
        }

        private ModelDiffApplyResult ApplyObjects(
            List<ModelObject> objects,
            ApplyDirection direction)
        {
            var result = new ModelDiffApplyResult();

            if (objects == null || objects.Count == 0)
                return result;

            if (application == null || targetPersistenceUnit == null)
                throw new InvalidOperationException("Aktarim icin erwin oturumu hazir degil.");

            SCAPI.Session session = null;
            object transaction = null;

            try
            {
                session = application.Sessions.Add();

                session.Open(
                    targetPersistenceUnit,
                    SCAPI.SC_SessionLevel.SCD_SL_M0);

                transaction = session.BeginNamedTransaction("Apply Model Diff");

                foreach (var obj in objects)
                {
                    ApplyObject(
                        session,
                        obj,
                        null,
                        direction,
                        result);
                }

                session.CommitTransaction(transaction);
                transaction = null;

                SaveTargetPersistenceUnit();

                return result;
            }
            catch
            {
                if (session != null && transaction != null)
                {
                    try
                    {
                        session.RollbackTransaction(transaction);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
            finally
            {
                if (session != null)
                {
                    try
                    {
                        session.Close();
                    }
                    catch
                    {
                    }

                    try
                    {
                        application.Sessions.Remove(session);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void ApplyObject(
            SCAPI.Session session,
            ModelObject source,
            SCAPI.ModelObject parentEntity,
            ApplyDirection direction,
            ModelDiffApplyResult result)
        {
            if (source == null)
                return;

            string className = source.getoClassName();

            if (className == "Entity")
            {
                ApplyEntity(
                    session,
                    source,
                    direction,
                    result);

                return;
            }

            if (className == "Attribute")
            {
                ApplyAttribute(
                    session,
                    source,
                    parentEntity,
                    direction,
                    result);
            }
        }

        private void ApplyEntity(
            SCAPI.Session session,
            ModelObject sourceEntity,
            ApplyDirection direction,
            ModelDiffApplyResult result)
        {
            SCAPI.ModelObject targetEntity =
                FindEntityByCompareName(
                    session,
                    sourceEntity.getoName());

            if (targetEntity == null)
            {
                string newEntityName =
                    ConvertEntityNameForTarget(
                        sourceEntity.getoName(),
                        direction);

                targetEntity =
                    CreateEntity(
                        session,
                        newEntityName,
                        result);
            }

            if (targetEntity == null)
                return;

            ApplyProperties(
                targetEntity,
                sourceEntity.getoObjectProperty(),
                direction,
                result);

            var children = sourceEntity.getoModelObject();
            if (children == null)
                return;

            foreach (var child in children)
            {
                ApplyObject(
                    session,
                    child,
                    targetEntity,
                    direction,
                    result);
            }
        }

        private void ApplyAttribute(
            SCAPI.Session session,
            ModelObject sourceAttribute,
            SCAPI.ModelObject parentEntity,
            ApplyDirection direction,
            ModelDiffApplyResult result)
        {
            if (parentEntity == null)
                return;

            SCAPI.ModelObject targetAttribute =
                FindAttributeByName(
                    session,
                    parentEntity,
                    sourceAttribute.getoName());

            if (targetAttribute == null)
            {
                targetAttribute =
                    CreateAttribute(
                        session,
                        parentEntity,
                        sourceAttribute.getoName(),
                        result);
            }

            if (targetAttribute == null)
                return;

            ApplyProperties(
                targetAttribute,
                sourceAttribute.getoObjectProperty(),
                direction,
                result);
        }

        private SCAPI.ModelObject CreateEntity(
            SCAPI.Session session,
            string name,
            ModelDiffApplyResult result)
        {
            SCAPI.ModelObjects entities =
                session.ModelObjects.Collect(session.ModelObjects.Root.ObjectId);

            SCAPI.ModelObject entity = entities.Add("Entity", Type.Missing);

            TrySetNameProperties(entity, name);
            result.CreatedObjects++;

            return entity;
        }

        private SCAPI.ModelObject CreateAttribute(
            SCAPI.Session session,
            SCAPI.ModelObject entity,
            string name,
            ModelDiffApplyResult result)
        {
            SCAPI.ModelObjects attributes =
                session.ModelObjects.Collect(entity.ObjectId);

            SCAPI.ModelObject attribute = attributes.Add("Attribute", Type.Missing);

            TrySetNameProperties(attribute, name);
            result.CreatedObjects++;

            return attribute;
        }

        private void ApplyProperties(
            SCAPI.ModelObject targetObject,
            List<ObjectProperty> properties,
            ApplyDirection direction,
            ModelDiffApplyResult result)
        {
            if (properties == null)
                return;

            foreach (var property in properties)
            {
                string valueToApply =
                    GetValueToApply(
                        property,
                        direction);

                if (valueToApply == null || valueToApply == "<NOT FOUND>")
                    continue;

                bool isSet = TrySetProperty(
                    targetObject,
                    property,
                    valueToApply,
                    result);

                if (isSet)
                    result.UpdatedProperties++;
            }
        }

        private static string GetValueToApply(
            ObjectProperty property,
            ApplyDirection direction)
        {
            if (direction == ApplyDirection.LeftToRight)
                return property.getLeftValue();

            return property.getRightValue();
        }

        private bool TrySetProperty(
            SCAPI.ModelObject targetObject,
            ObjectProperty sourceProperty,
            string value,
            ModelDiffApplyResult result)
        {
            try
            {
                SCAPI.ModelProperty targetProperty =
                    FindTargetProperty(
                        targetObject,
                        sourceProperty);

                if (targetProperty == null)
                {
                    result.SkippedProperties++;
                    result.Messages.Add(
                        "Property bulunamadi: " +
                        sourceProperty.getoPropertyClassName());
                    return false;
                }

                targetProperty.Value = ConvertValueForTarget(
                    targetProperty,
                    value);

                return true;
            }
            catch (Exception ex)
            {
                result.FailedOperations++;
                result.Messages.Add(
                    sourceProperty.getoPropertyClassName() +
                    " set edilemedi: " +
                    ex.Message);

                return false;
            }
        }

        private SCAPI.ModelProperty FindTargetProperty(
            SCAPI.ModelObject targetObject,
            ObjectProperty sourceProperty)
        {
            string sourceClassId =
                sourceProperty.getoPropertyClassID();

            string sourceClassName =
                sourceProperty.getoPropertyClassName();

            string normalizedSourceName =
                NormalizePropertyName(sourceClassName);

            foreach (SCAPI.ModelProperty property in targetObject.Properties)
            {
                string targetClassId =
                    property.ClassId == null
                        ? null
                        : property.ClassId.ToString();

                string targetClassName =
                    property.ClassName == null
                        ? null
                        : property.ClassName.ToString();

                if (!string.IsNullOrEmpty(sourceClassId) &&
                    string.Equals(
                        targetClassId,
                        sourceClassId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }

                if (!string.IsNullOrEmpty(sourceClassName) &&
                    string.Equals(
                        targetClassName,
                        sourceClassName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }

                if (!string.IsNullOrEmpty(normalizedSourceName) &&
                    string.Equals(
                        NormalizePropertyName(targetClassName),
                        normalizedSourceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(sourceClassId))
                    return targetObject.Properties[sourceClassId];
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrEmpty(sourceClassName))
                    return targetObject.Properties[sourceClassName];
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrEmpty(normalizedSourceName))
                    return targetObject.Properties[normalizedSourceName];
            }
            catch
            {
            }

            return null;
        }

        private void TrySetNameProperties(
            SCAPI.ModelObject obj,
            string name)
        {
            TrySetPropertyByName(obj, "Name", name);
            TrySetPropertyByName(obj, "Physical_Name", name);
            TrySetPropertyByName(obj, "User_Formatted_Physical_Name", name);
        }

        private bool TrySetPropertyByName(
            SCAPI.ModelObject obj,
            string propertyName,
            object value)
        {
            try
            {
                obj.Properties[propertyName].Value = value;
                return true;
            }
            catch
            {
            }

            try
            {
                foreach (SCAPI.ModelProperty prop in obj.Properties)
                {
                    if (string.Equals(
                        prop.ClassName,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        prop.Value = value;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private SCAPI.ModelObject FindEntityByCompareName(
            SCAPI.Session session,
            string sourceEntityName)
        {
            SCAPI.ModelObjects entities =
                session.ModelObjects.Collect(
                    session.ModelObjects.Root,
                    "Entity",
                    1);

            foreach (SCAPI.ModelObject entity in entities)
            {
                string targetName =
                    GetProperty(
                        entity,
                        "Name");

                if (IsSameEntityName(
                    targetName,
                    sourceEntityName))
                {
                    return entity;
                }
            }

            return null;
        }

        private SCAPI.ModelObject FindAttributeByName(
            SCAPI.Session session,
            SCAPI.ModelObject entity,
            string attributeName)
        {
            SCAPI.ModelObjects attributes =
                session.ModelObjects.Collect(
                    entity,
                    "Attribute",
                    1);

            foreach (SCAPI.ModelObject attribute in attributes)
            {
                string targetName =
                    GetProperty(
                        attribute,
                        "Name");

                if (string.Equals(
                    targetName,
                    attributeName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return attribute;
                }
            }

            return null;
        }

        private static bool IsSameEntityName(
            string targetName,
            string sourceName)
        {
            return string.Equals(
                NormalizeEntityName(targetName),
                NormalizeEntityName(sourceName),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeEntityName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            if (name.Length > 1)
                return name.Substring(1);

            return name;
        }

        private static string GetProperty(
            SCAPI.ModelObject obj,
            string propertyName)
        {
            try
            {
                object value = obj.Properties[propertyName].Value;
                return value == null ? null : value.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string ConvertEntityNameForTarget(
            string sourceName,
            ApplyDirection direction)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                return sourceName;

            sourceName = sourceName.Trim();

            if (sourceName.Length < 2)
                return sourceName;

            string rest = sourceName.Substring(1);

            if (direction == ApplyDirection.LeftToRight)
                return "I" + rest;

            return "D" + rest;
        }

        private void SaveTargetPersistenceUnit()
        {
            string locator = GetPersistenceUnitLocator();

            if (string.IsNullOrWhiteSpace(locator))
            {
                targetPersistenceUnit.Save(Type.Missing, Type.Missing);
                return;
            }

            string disposition = locator.StartsWith(
                "mart://",
                StringComparison.OrdinalIgnoreCase)
                ? "OVM=Yes;OVS=Yes"
                : "OVF=Yes";

            targetPersistenceUnit.Save(locator, disposition);
        }

        private string GetPersistenceUnitLocator()
        {
            try
            {
                SCAPI.PropertyBag bag =
                    targetPersistenceUnit.PropertyBag["Locator;Hidden_Model"];

                object value = bag.Value["Locator"];
                return value == null ? null : value.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizePropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return string.Empty;

            string trimmedName = propertyName.Trim();
            int dotIndex = trimmedName.LastIndexOf('.');

            if (dotIndex >= 0 && dotIndex < trimmedName.Length - 1)
                return trimmedName.Substring(dotIndex + 1);

            return trimmedName;
        }

        private static object ConvertValueForTarget(
            SCAPI.ModelProperty targetProperty,
            string value)
        {
            if (value == null)
                return null;

            try
            {
                bool isScalar =
                    (targetProperty.Flags & SCAPI.SC_ModelPropertyFlags.SCD_MPF_SCALAR) != 0;

                SCAPI.SC_ValueTypes valueType = isScalar
                    ? targetProperty.DataType
                    : targetProperty.DataType[0];

                switch (valueType)
                {
                    case SCAPI.SC_ValueTypes.SCVT_I1:
                    case SCAPI.SC_ValueTypes.SCVT_I2:
                    case SCAPI.SC_ValueTypes.SCVT_I4:
                    case SCAPI.SC_ValueTypes.SCVT_INT:
                        return int.Parse(value);

                    case SCAPI.SC_ValueTypes.SCVT_UI1:
                    case SCAPI.SC_ValueTypes.SCVT_UI2:
                    case SCAPI.SC_ValueTypes.SCVT_UI4:
                    case SCAPI.SC_ValueTypes.SCVT_UINT:
                        return uint.Parse(value);

                    case SCAPI.SC_ValueTypes.SCVT_I8:
                        return long.Parse(value);

                    case SCAPI.SC_ValueTypes.SCVT_UI8:
                        return ulong.Parse(value);

                    case SCAPI.SC_ValueTypes.SCVT_R4:
                    case SCAPI.SC_ValueTypes.SCVT_R8:
                    case SCAPI.SC_ValueTypes.SCVT_CURRENCY:
                        return double.Parse(value);

                    case SCAPI.SC_ValueTypes.SCVT_BOOLEAN:
                        return bool.Parse(value);

                    case SCAPI.SC_ValueTypes.SCVT_DATE:
                        return DateTime.Parse(value);
                }
            }
            catch
            {
            }

            return value;
        }
    }

    internal sealed class ModelDiffApplyResult
    {
        public ModelDiffApplyResult()
        {
            Messages = new List<string>();
        }

        public int CreatedObjects { get; set; }

        public int UpdatedProperties { get; set; }

        public int SkippedProperties { get; set; }

        public int FailedOperations { get; set; }

        public List<string> Messages { get; private set; }

        public int AppliedChanges
        {
            get
            {
                return CreatedObjects + UpdatedProperties;
            }
        }

        public string ToSummary()
        {
            var parts = new[]
            {
                CreatedObjects + " nesne olusturuldu",
                UpdatedProperties + " property guncellendi",
                SkippedProperties + " property atlandi",
                FailedOperations + " hata"
            };

            return string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}
