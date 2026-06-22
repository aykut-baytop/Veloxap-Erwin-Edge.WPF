using System;
using System.Collections.Generic;
using System.Linq;

namespace Veloxap.AddIn.Erwin.Models.Integrate
{
    internal class ModelDiffResult
    {
        public ModelDiffResult()
        {
            OnlyInLeft = new List<ModelObject>();
            OnlyInRight = new List<ModelObject>();
        }

        public List<ModelObject> OnlyInLeft { get; set; }

        public List<ModelObject> OnlyInRight { get; set; }
    }

    internal static class ModelObjectComparer
    {
        public static ModelDiffResult Compare(ModelInfo left, ModelInfo right)
        {
            var result = new ModelDiffResult();

            result.OnlyInLeft = FindMissingOrDifferentNodes(
                left == null ? null : left.getoModelObject(),
                right == null ? null : right.getoModelObject(),
                true);

            result.OnlyInRight = FindMissingOrDifferentNodes(
                right == null ? null : right.getoModelObject(),
                left == null ? null : left.getoModelObject(),
                false);

            return result;
        }

        private static List<ModelObject> FindMissingOrDifferentNodes(
            List<ModelObject> source,
            List<ModelObject> target,
            bool sourceIsLeft)
        {
            var result = new List<ModelObject>();

            if (source == null)
                return result;

            foreach (var sourceObject in source)
            {
                if (!IsAllowedClass(sourceObject))
                    continue;

                var targetObject = target?
                    .Where(IsAllowedClass)
                    .FirstOrDefault(x => IsSameObject(sourceObject, x));

                if (targetObject == null)
                {
                    result.Add(CloneObjectFiltered(sourceObject, sourceIsLeft));
                    continue;
                }

                var propertyDiff = CompareFilteredProperties(
                    sourceObject.getoObjectProperty(),
                    targetObject.getoObjectProperty(),
                    sourceIsLeft);

                var childDiff = FindMissingOrDifferentNodes(
                    sourceObject.getoModelObject(),
                    targetObject.getoModelObject(),
                    sourceIsLeft);

                if (propertyDiff.Count == 0 && childDiff.Count == 0)
                    continue;

                var diffNode = new ModelObject();

                diffNode.setoObjectId(sourceObject.getoObjectId());
                diffNode.setoClassName(sourceObject.getoClassName());
                diffNode.setoName(sourceObject.getoName());
                diffNode.setoObjectProperty(propertyDiff);
                diffNode.setoModelObjects(childDiff);

                result.Add(diffNode);
            }

            return result;
        }

        private static bool IsAllowedClass(ModelObject obj)
        {
            if (obj == null)
                return false;

            string className = obj.getoClassName();

            return className == "Entity" ||
                   className == "Attribute";
        }

        private static bool IsSameObject(ModelObject left, ModelObject right)
        {
            if (left == null || right == null)
                return false;

            if (left.getoClassName() != right.getoClassName())
                return false;

            string leftName = NormalizeObjectName(
                left.getoClassName(),
                left.getoName());

            string rightName = NormalizeObjectName(
                right.getoClassName(),
                right.getoName());

            return string.Equals(
                leftName,
                rightName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeObjectName(
            string className,
            string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return string.Empty;

            if (className == "Entity" && objectName.Length > 1)
                return objectName.Substring(1);

            return objectName;
        }

        private static List<ObjectProperty> CompareFilteredProperties(
            List<ObjectProperty> sourceProps,
            List<ObjectProperty> targetProps,
            bool sourceIsLeft)
        {
            var result = new List<ObjectProperty>();

            if (sourceProps == null)
                return result;

            foreach (var sourceProp in sourceProps.Where(IsAllowedProperty))
            {
                var targetProp = targetProps?
                    .Where(IsAllowedProperty)
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.getoPropertyClassName(),
                            sourceProp.getoPropertyClassName(),
                            StringComparison.OrdinalIgnoreCase));

                if (targetProp == null)
                {
                    var diffProp = CloneProperty(sourceProp);

                    SetLeftRightValues(
                        diffProp,
                        sourceProp.getoPropertyValue(),
                        null,
                        sourceIsLeft);

                    result.Add(diffProp);
                    continue;
                }

                if (string.Equals(
                    sourceProp.getoPropertyValue(),
                    targetProp.getoPropertyValue(),
                    StringComparison.Ordinal))
                {
                    continue;
                }

                var changedProp = CloneProperty(sourceProp);

                SetLeftRightValues(
                    changedProp,
                    sourceProp.getoPropertyValue(),
                    targetProp.getoPropertyValue(),
                    sourceIsLeft);

                result.Add(changedProp);
            }

            return result;
        }

        private static void SetLeftRightValues(
            ObjectProperty property,
            string sourceValue,
            string targetValue,
            bool sourceIsLeft)
        {
            if (sourceIsLeft)
            {
                property.setLeftValue(sourceValue);
                property.setRightValue(targetValue);
            }
            else
            {
                property.setLeftValue(targetValue);
                property.setRightValue(sourceValue);
            }
        }

        private static bool IsAllowedProperty(ObjectProperty prop)
        {
            if (prop == null)
                return false;

            string propertyName = prop.getoPropertyClassName();

            if (string.IsNullOrEmpty(propertyName))
                return false;

            if (string.Equals(
                propertyName,
                "Comment",
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return propertyName.Contains(".");
        }

        private static ModelObject CloneObjectFiltered(
            ModelObject source,
            bool sourceIsLeft)
        {
            var clone = new ModelObject();

            clone.setoObjectId(source.getoObjectId());
            clone.setoClassName(source.getoClassName());
            clone.setoName(source.getoName());

            clone.setoObjectProperty(
                source.getoObjectProperty()?
                    .Where(IsAllowedProperty)
                    .Select(x => ClonePropertyWithSideValue(x, sourceIsLeft))
                    .ToList());

            clone.setoModelObjects(
                source.getoModelObject()?
                    .Where(IsAllowedClass)
                    .Select(x => CloneObjectFiltered(x, sourceIsLeft))
                    .ToList());

            return clone;
        }

        private static ObjectProperty ClonePropertyWithSideValue(
            ObjectProperty source,
            bool sourceIsLeft)
        {
            var clone = CloneProperty(source);

            if (sourceIsLeft)
            {
                clone.setLeftValue(source.getoPropertyValue());
                clone.setRightValue(null);
            }
            else
            {
                clone.setLeftValue(null);
                clone.setRightValue(source.getoPropertyValue());
            }

            return clone;
        }

        private static ObjectProperty CloneProperty(ObjectProperty source)
        {
            var clone = new ObjectProperty();

            clone.setObjectProperty(
                source.getoPropertyClassID(),
                source.getoPropertyClassName(),
                source.getoPropertyType(),
                source.getoPropertyFormatAsString(),
                source.getoPropertyValue());

            return clone;
        }
    }
}
