using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VeloxapEDGEWpfLib.Models;

namespace VeloxapEDGEWpfLib.Services
{
    internal sealed class TableUdpSecurityService
    {
        private static readonly string[] AvailabilityPropertyNames =
        {
            "erisebilirlik",
            "erisilebilirlik"
        };

        private static readonly string[] IntegrityPropertyNames =
        {
            "butunluk"
        };

        private static readonly string[] ConfidentialityPropertyNames =
        {
            "gizlilikseviyesi"
        };

        private static readonly string[] AssetValuePropertyNames =
        {
            "varlikdegeri"
        };

        private readonly SCAPI.Application application;
        private readonly SCAPI.PersistenceUnit persistenceUnit;

        public TableUdpSecurityService(
            SCAPI.Application application,
            SCAPI.PersistenceUnit persistenceUnit)
        {
            this.application = application;
            this.persistenceUnit = persistenceUnit;
        }

        public static int CountCalculableTables(ModelInfo modelInfo)
        {
            return BuildCalculations(modelInfo, null).Count;
        }

        public TableUdpSecurityApplyResult Apply(ModelInfo modelInfo)
        {
            var result = new TableUdpSecurityApplyResult();
            var calculations = BuildCalculations(modelInfo, result);

            if (calculations.Count == 0)
                return result;

            if (application == null || persistenceUnit == null)
                throw new InvalidOperationException("Varlik degeri guncellemesi icin erwin oturumu hazir degil.");

            SCAPI.Session session = null;
            object transaction = null;

            try
            {
                session = application.Sessions.Add();
                session.Open(
                    persistenceUnit,
                    SCAPI.SC_SessionLevel.SCD_SL_M0);

                transaction = session.BeginNamedTransaction("Calculate Table UDP Asset Values");

                foreach (var calculation in calculations)
                    ApplyCalculation(session, calculation, result);

                session.CommitTransaction(transaction);
                transaction = null;

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

        private void ApplyCalculation(
            SCAPI.Session session,
            TableUdpSecurityCalculation calculation,
            TableUdpSecurityApplyResult result)
        {
            SCAPI.ModelObject targetObject = FindTargetTable(session, calculation);
            if (targetObject == null)
            {
                result.SkippedTables++;
                result.Messages.Add(calculation.TableName + ": tablo erwin oturumunda bulunamadi.");
                return;
            }

            SCAPI.ModelProperty targetProperty = FindTargetProperty(
                targetObject,
                AssetValuePropertyNames);

            if (targetProperty == null)
            {
                result.SkippedTables++;
                result.Messages.Add(calculation.TableName + ": Varlik_Degeri UDP bulunamadi.");
                return;
            }

            string writtenValue;
            if (!TrySetPropertyValue(
                targetProperty,
                calculation.ResultLevel,
                calculation.ResultValue,
                out writtenValue))
            {
                result.FailedTables++;
                result.Messages.Add(calculation.TableName + ": Varlik_Degeri yazilamadi.");
                return;
            }

            calculation.AssetValueProperty.setoPropertyValue(writtenValue);
            calculation.AssetValueProperty.setoPropertyFormatAsString(calculation.ResultValue);
            result.UpdatedTables++;
        }

        private SCAPI.ModelObject FindTargetTable(
            SCAPI.Session session,
            TableUdpSecurityCalculation calculation)
        {
            if (!string.IsNullOrWhiteSpace(calculation.TableObjectId))
            {
                try
                {
                    return session.ModelObjects[calculation.TableObjectId];
                }
                catch
                {
                }
            }

            try
            {
                SCAPI.ModelObjects entities =
                    session.ModelObjects.Collect(
                        session.ModelObjects.Root,
                        "Entity",
                        1);

                foreach (SCAPI.ModelObject entity in entities)
                {
                    if (string.Equals(
                        entity.ObjectId,
                        calculation.TableObjectId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return entity;
                    }

                    if (string.Equals(
                        entity.Name,
                        calculation.TableName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return entity;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static SCAPI.ModelProperty FindTargetProperty(
            SCAPI.ModelObject targetObject,
            string[] normalizedNames)
        {
            try
            {
                foreach (SCAPI.ModelProperty property in targetObject.Properties)
                {
                    string normalizedName = NormalizePropertyName(property.ClassName);

                    if (normalizedNames.Any(name => IsPropertyMatch(normalizedName, name)))
                        return property;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TrySetPropertyValue(
            SCAPI.ModelProperty property,
            int level,
            string resultValue,
            out string writtenValue)
        {
            object[] attempts =
            {
                resultValue,
                level.ToString(CultureInfo.InvariantCulture),
                level
            };

            foreach (object attempt in attempts)
            {
                try
                {
                    property.Value = ConvertValueForTarget(property, attempt);
                    writtenValue = Convert.ToString(attempt, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                }
            }

            writtenValue = string.Empty;
            return false;
        }

        private static object ConvertValueForTarget(
            SCAPI.ModelProperty targetProperty,
            object value)
        {
            if (value == null)
                return null;

            string textValue = Convert.ToString(value, CultureInfo.InvariantCulture);

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
                        return int.Parse(textValue, CultureInfo.InvariantCulture);

                    case SCAPI.SC_ValueTypes.SCVT_UI1:
                    case SCAPI.SC_ValueTypes.SCVT_UI2:
                    case SCAPI.SC_ValueTypes.SCVT_UI4:
                    case SCAPI.SC_ValueTypes.SCVT_UINT:
                        return uint.Parse(textValue, CultureInfo.InvariantCulture);

                    case SCAPI.SC_ValueTypes.SCVT_I8:
                        return long.Parse(textValue, CultureInfo.InvariantCulture);

                    case SCAPI.SC_ValueTypes.SCVT_UI8:
                        return ulong.Parse(textValue, CultureInfo.InvariantCulture);

                    case SCAPI.SC_ValueTypes.SCVT_R4:
                    case SCAPI.SC_ValueTypes.SCVT_R8:
                    case SCAPI.SC_ValueTypes.SCVT_CURRENCY:
                        return double.Parse(textValue, CultureInfo.InvariantCulture);

                    case SCAPI.SC_ValueTypes.SCVT_BOOLEAN:
                        return bool.Parse(textValue);

                    case SCAPI.SC_ValueTypes.SCVT_DATE:
                        return DateTime.Parse(textValue, CultureInfo.CurrentCulture);
                }
            }
            catch
            {
            }

            return value;
        }

        private static List<TableUdpSecurityCalculation> BuildCalculations(
            ModelInfo modelInfo,
            TableUdpSecurityApplyResult result)
        {
            var calculations = new List<TableUdpSecurityCalculation>();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            foreach (var table in EnumerateTables(objects))
            {
                var properties = table.getoObjectProperty();
                if (properties == null || properties.Count == 0)
                    continue;

                ObjectProperty availability = FindProperty(properties, AvailabilityPropertyNames);
                ObjectProperty integrity = FindProperty(properties, IntegrityPropertyNames);
                ObjectProperty confidentiality = FindProperty(properties, ConfidentialityPropertyNames);
                ObjectProperty assetValue = FindProperty(properties, AssetValuePropertyNames);

                var missing = new List<string>();
                int availabilityLevel = 0;
                int integrityLevel = 0;
                int confidentialityLevel = 0;

                if (availability == null)
                    missing.Add("Erisebilirlik");

                if (integrity == null)
                    missing.Add("Butunluk");

                if (confidentiality == null)
                    missing.Add("Gizlilik_Seviyesi");

                if (assetValue == null)
                    missing.Add("Varlik_Degeri");

                if (availability != null && !TryResolveLevel(availability, out availabilityLevel))
                    missing.Add("Erisebilirlik seviyesi");

                if (integrity != null && !TryResolveLevel(integrity, out integrityLevel))
                    missing.Add("Butunluk seviyesi");

                if (confidentiality != null && !TryResolveLevel(confidentiality, out confidentialityLevel))
                    missing.Add("Gizlilik_Seviyesi seviyesi");

                if (missing.Count > 0)
                {
                    if (result != null)
                    {
                        result.SkippedTables++;
                        result.Messages.Add(
                            Safe(table.getoName(), "(adsiz tablo)") +
                            ": eksik/okunamayan alanlar - " +
                            string.Join(", ", missing));
                    }

                    continue;
                }

                double average = (availabilityLevel + integrityLevel + confidentialityLevel) / 3.0;
                int resultLevel = ClampLevel(
                    (int)Math.Round(
                        average,
                        MidpointRounding.AwayFromZero));

                calculations.Add(new TableUdpSecurityCalculation
                {
                    TableObjectId = table.getoObjectId(),
                    TableName = Safe(table.getoName(), "(adsiz tablo)"),
                    AvailabilityLevel = availabilityLevel,
                    IntegrityLevel = integrityLevel,
                    ConfidentialityLevel = confidentialityLevel,
                    Average = average,
                    ResultLevel = resultLevel,
                    ResultValue = BuildAssetValue(resultLevel),
                    AssetValueProperty = assetValue
                });
            }

            return calculations;
        }

        private static IEnumerable<ModelObject> EnumerateTables(IEnumerable<ModelObject> objects)
        {
            if (objects == null)
                yield break;

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                if (string.Equals(obj.getoClassName(), "Entity", StringComparison.OrdinalIgnoreCase))
                    yield return obj;

                var children = obj.getoModelObject();
                if (children == null)
                    continue;

                foreach (var child in EnumerateTables(children))
                    yield return child;
            }
        }

        private static ObjectProperty FindProperty(
            IEnumerable<ObjectProperty> properties,
            string[] normalizedNames)
        {
            return properties.FirstOrDefault(property =>
            {
                string normalizedName = NormalizePropertyName(property.getoPropertyClassName());
                return normalizedNames.Any(name => IsPropertyMatch(normalizedName, name));
            });
        }

        private static bool IsPropertyMatch(string normalizedName, string expectedName)
        {
            return string.Equals(
                       normalizedName,
                       expectedName,
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.EndsWith(
                       expectedName,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveLevel(ObjectProperty property, out int level)
        {
            if (TryResolveLevelText(property.getoPropertyFormatAsString(), out level))
                return true;

            if (TryResolveLevelText(property.getoPropertyValue(), out level))
                return true;

            level = 0;
            return false;
        }

        private static bool TryResolveLevelText(string value, out int level)
        {
            level = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (TryResolveNumericLevel(value.Trim(), out level))
                return true;

            string normalized = NormalizeValue(value);

            if (TryResolveNumericLevel(normalized, out level))
                return true;

            if (normalized.Contains("cokgizli") ||
                normalized.Contains("yuksek") ||
                normalized.Contains("kritik"))
            {
                level = 4;
                return true;
            }

            if (normalized.Contains("gizli") ||
                normalized.Contains("orta"))
            {
                level = 3;
                return true;
            }

            if (normalized.Contains("hizmeteozel") ||
                normalized.Contains("dusuk"))
            {
                level = 2;
                return true;
            }

            if (normalized.Contains("kamuyaacik") ||
                normalized.Contains("bilgi"))
            {
                level = 1;
                return true;
            }

            return false;
        }

        private static bool TryResolveNumericLevel(string value, out int level)
        {
            level = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            double numericValue;
            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out numericValue) &&
                !double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out numericValue))
            {
                return false;
            }

            int roundedLevel = (int)Math.Round(
                numericValue,
                MidpointRounding.AwayFromZero);

            if (roundedLevel >= 1 && roundedLevel <= 4)
            {
                level = roundedLevel;
                return true;
            }

            if (roundedLevel == 0)
            {
                level = 1;
                return true;
            }

            return false;
        }

        private static int ClampLevel(int level)
        {
            if (level < 1)
                return 1;

            if (level > 4)
                return 4;

            return level;
        }

        private static string BuildAssetValue(int level)
        {
            switch (ClampLevel(level))
            {
                case 4:
                    return "4-\u00c7ok Gizli/Y\u00fcksek";
                case 3:
                    return "3-Gizli/Orta";
                case 2:
                    return "2-Hizmete \u00d6zel/D\u00fc\u015f\u00fck";
                default:
                    return "1-Kamuya A\u00e7\u0131k/Bilgi";
            }
        }

        private static string NormalizePropertyName(string value)
        {
            return NormalizeText(value, keepDigits: false);
        }

        private static string NormalizeValue(string value)
        {
            return NormalizeText(value, keepDigits: true);
        }

        private static string NormalizeText(string value, bool keepDigits)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (char c in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue;

                char folded = FoldTurkishCharacter(char.ToLowerInvariant(c));

                if (char.IsLetter(folded) || (keepDigits && char.IsDigit(folded)))
                    builder.Append(folded);
            }

            return builder.ToString();
        }

        private static char FoldTurkishCharacter(char c)
        {
            switch (c)
            {
                case '\u0131':
                    return 'i';
                case '\u011f':
                    return 'g';
                case '\u015f':
                    return 's';
                case '\u00e7':
                    return 'c';
                case '\u00f6':
                    return 'o';
                case '\u00fc':
                    return 'u';
                default:
                    return c;
            }
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
        }
    }

    internal sealed class TableUdpSecurityApplyResult
    {
        public TableUdpSecurityApplyResult()
        {
            Messages = new List<string>();
        }

        public int UpdatedTables { get; set; }

        public int SkippedTables { get; set; }

        public int FailedTables { get; set; }

        public List<string> Messages { get; private set; }

        public string ToSummary()
        {
            return UpdatedTables + " tablo guncellendi, " +
                   SkippedTables + " tablo atlandi, " +
                   FailedTables + " hata";
        }
    }

    internal sealed class TableUdpSecurityCalculation
    {
        public string TableObjectId { get; set; }

        public string TableName { get; set; }

        public int AvailabilityLevel { get; set; }

        public int IntegrityLevel { get; set; }

        public int ConfidentialityLevel { get; set; }

        public double Average { get; set; }

        public int ResultLevel { get; set; }

        public string ResultValue { get; set; }

        public ObjectProperty AssetValueProperty { get; set; }
    }
}
