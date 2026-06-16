using System;
using System.Collections.Generic;
using VeloxapEDGEWpfLib.Models;
using VeloxapEDGEWpfLib.Models.Integrate;

namespace VeloxapEDGEWpfLib.Services
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

        public void ApplyLeftToRight(ModelDiffResult diff)
        {
            if (diff == null)
                return;

            ApplyObjects(
                diff.OnlyInLeft,
                ApplyDirection.LeftToRight);
        }

        public void ApplyRightToLeft(ModelDiffResult diff)
        {
            if (diff == null)
                return;

            ApplyObjects(
                diff.OnlyInRight,
                ApplyDirection.RightToLeft);
        }

        public void Apply(
            ModelDiffResult diff,
            ApplyDirection direction)
        {
            if (direction == ApplyDirection.LeftToRight)
                ApplyLeftToRight(diff);
            else
                ApplyRightToLeft(diff);
        }

        private void ApplyObjects(
            List<ModelObject> objects,
            ApplyDirection direction)
        {
            if (objects == null || objects.Count == 0)
                return;

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
                        direction);
                }

                session.CommitTransaction(transaction);
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
            ApplyDirection direction)
        {
            if (source == null)
                return;

            string className = source.getoClassName();

            if (className == "Entity")
            {
                ApplyEntity(
                    session,
                    source,
                    direction);

                return;
            }

            if (className == "Attribute")
            {
                ApplyAttribute(
                    session,
                    source,
                    parentEntity,
                    direction);
            }
        }

        private void ApplyEntity(
            SCAPI.Session session,
            ModelObject sourceEntity,
            ApplyDirection direction)
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
                        newEntityName);
            }

            if (targetEntity == null)
                return;

            ApplyProperties(
                targetEntity,
                sourceEntity.getoObjectProperty(),
                direction);

            var children = sourceEntity.getoModelObject();
            if (children == null)
                return;

            foreach (var child in children)
            {
                ApplyObject(
                    session,
                    child,
                    targetEntity,
                    direction);
            }
        }

        private void ApplyAttribute(
            SCAPI.Session session,
            ModelObject sourceAttribute,
            SCAPI.ModelObject parentEntity,
            ApplyDirection direction)
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
                        sourceAttribute.getoName());
            }

            if (targetAttribute == null)
                return;

            ApplyProperties(
                targetAttribute,
                sourceAttribute.getoObjectProperty(),
                direction);
        }

        private SCAPI.ModelObject CreateEntity(
            SCAPI.Session session,
            string name)
        {
            SCAPI.ModelObjects entities =
                session.ModelObjects.Collect(
                    session.ModelObjects.Root,
                    "Entity",
                    1);

            SCAPI.ModelObject entity = entities.Add("Entity");

            TrySetPropertyByName(entity, "Name", name);
            TrySetPropertyByName(entity, "Physical_Name", name);

            return entity;
        }

        private SCAPI.ModelObject CreateAttribute(
            SCAPI.Session session,
            SCAPI.ModelObject entity,
            string name)
        {
            SCAPI.ModelObjects attributes =
                session.ModelObjects.Collect(
                    entity,
                    "Attribute",
                    1);

            SCAPI.ModelObject attribute = attributes.Add("Attribute");

            TrySetPropertyByName(attribute, "Name", name);
            TrySetPropertyByName(attribute, "Physical_Name", name);

            return attribute;
        }

        private void ApplyProperties(
            SCAPI.ModelObject targetObject,
            List<ObjectProperty> properties,
            ApplyDirection direction)
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

                TrySetProperty(
                    targetObject,
                    property,
                    valueToApply);
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

        private void TrySetProperty(
            SCAPI.ModelObject targetObject,
            ObjectProperty sourceProperty,
            string value)
        {
            try
            {
                SCAPI.ModelProperty targetProperty =
                    FindTargetProperty(
                        targetObject,
                        sourceProperty);

                if (targetProperty == null)
                    return;

                targetProperty.Value = value;
            }
            catch
            {
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

            return null;
        }

        private void TrySetPropertyByName(
            SCAPI.ModelObject obj,
            string propertyName,
            object value)
        {
            try
            {
                obj.Properties[propertyName].Value = value;
            }
            catch
            {
            }
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
    }
}
