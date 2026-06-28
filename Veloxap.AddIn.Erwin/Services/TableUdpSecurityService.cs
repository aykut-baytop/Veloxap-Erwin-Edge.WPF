using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Veloxap.AddIn.Erwin.Models;

namespace Veloxap.AddIn.Erwin.Services
{
    internal sealed class TableUdpSecurityService
    {
        private static readonly string[] AvailabilityPropertyNames =
        {
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
            "veridegeri"
        };

        private static readonly string[] BusinessProcessLevelPropertyNames =
        {
            "issureciseviyesi"
        };

        private static readonly string[] BankRelativeValuePropertyNames =
        {
            "bankagorecedegeri"
        };

        private static readonly string[] PersonalDataPropertyNames =
        {
            "kisiselverimi"
        };

        private static readonly string[] SensitiveDataPropertyNames =
        {
            "hassasverimi"
        };

        private static readonly string[] SecurityClassValuePropertyNames =
        {
            "guvenliksinifidegeri"
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

        public TableUdpStartupApplyResult ApplyStartupCalculations(ModelInfo modelInfo)
        {
            var startupResult = new TableUdpStartupApplyResult(DateTime.Now);

            startupResult.AddOperation(
                "Veri Degeri",
                () => Apply(modelInfo));

            startupResult.AddOperation(
                "Banka Gorece Degeri",
                () => ApplyBankRelativeValue(modelInfo));

            startupResult.AddOperation(
                "Guvenlik Sinifi Degeri",
                () => ApplySecurityClassValue(modelInfo));

            startupResult.CompletedAt = DateTime.Now;
            return startupResult;
        }

        public static int CountCalculableTables(ModelInfo modelInfo)
        {
            return BuildCalculations(modelInfo, null).Count;
        }

        public static TableUdpApprovalScriptResult BuildApprovalScript(ModelInfo modelInfo)
        {
            var changes = BuildApprovalChanges(modelInfo);
            return new TableUdpApprovalScriptResult(changes, BuildApprovalScriptText(changes));
        }

        public static TableUdpCalculationPreview PreviewCalculation(
            ModelInfo modelInfo,
            string tableObjectId,
            string tableName,
            string normalizedUdpName)
        {
            ModelObject table = FindModelTable(modelInfo, tableObjectId, tableName);

            if (string.Equals(normalizedUdpName, "veridegeri", StringComparison.OrdinalIgnoreCase))
                return PreviewAssetValue(table);

            if (string.Equals(normalizedUdpName, "bankagorecedegeri", StringComparison.OrdinalIgnoreCase))
                return PreviewBankRelativeValue(table);

            if (string.Equals(normalizedUdpName, "guvenliksinifidegeri", StringComparison.OrdinalIgnoreCase))
                return PreviewSecurityClassValue(table);

            return TableUdpCalculationPreview.FromMessage("Bu UDP icin hesaplama bulunamadi.");
        }

        private static TableUdpCalculationPreview PreviewAssetValue(ModelObject table)
        {
            string tableName = table == null
                ? "(adsiz tablo)"
                : Safe(table.getoName(), "(adsiz tablo)");

            if (table == null)
                return TableUdpCalculationPreview.FromMessage("Tablo bulunamadi.");

            var properties = table.getoObjectProperty();
            if (properties == null || properties.Count == 0)
                return TableUdpCalculationPreview.FromMessage(tableName + ": UDP bulunamadi.");

            ObjectProperty availability = FindProperty(properties, AvailabilityPropertyNames);
            ObjectProperty integrity = FindProperty(properties, IntegrityPropertyNames);
            ObjectProperty confidentiality = FindProperty(properties, ConfidentialityPropertyNames);
            ObjectProperty assetValue = FindProperty(properties, AssetValuePropertyNames);

            var missing = new List<string>();
            int availabilityLevel = 0;
            int integrityLevel = 0;
            int confidentialityLevel = 0;

            if (availability == null)
                missing.Add("Erisilebilirlik");

            if (integrity == null)
                missing.Add("Butunluk");

            if (confidentiality == null)
                missing.Add("Gizlilik_Seviyesi");

            if (assetValue == null)
                missing.Add("Veri_Degeri");

            if (availability != null && !TryResolveLevel(availability, out availabilityLevel))
                missing.Add("Erisilebilirlik seviyesi");

            if (integrity != null && !TryResolveLevel(integrity, out integrityLevel))
                missing.Add("Butunluk seviyesi");

            if (confidentiality != null && !TryResolveLevel(confidentiality, out confidentialityLevel))
                missing.Add("Gizlilik_Seviyesi seviyesi");

            if (missing.Count > 0)
                return TableUdpCalculationPreview.FromMessage(
                    BuildPreviewMissingMessage(tableName, "eksik/okunamayan alanlar", missing));

            double average = (availabilityLevel + integrityLevel + confidentialityLevel) / 3.0;
            int resultLevel = ClampLevel(
                (int)Math.Round(
                    average,
                    MidpointRounding.AwayFromZero));

            var calculation = new TableUdpSecurityCalculation
            {
                TableObjectId = table.getoObjectId(),
                TableName = tableName,
                AvailabilityLevel = availabilityLevel,
                IntegrityLevel = integrityLevel,
                ConfidentialityLevel = confidentialityLevel,
                Average = average,
                ResultLevel = resultLevel,
                ResultValue = BuildAssetValue(resultLevel),
                AssetValueProperty = assetValue
            };

            return TableUdpCalculationPreview.FromCalculation(
                calculation.ToFormulaText(),
                calculation.ResultValue);
        }

        private static TableUdpCalculationPreview PreviewBankRelativeValue(ModelObject table)
        {
            string tableName = table == null
                ? "(adsiz tablo)"
                : Safe(table.getoName(), "(adsiz tablo)");

            if (table == null)
                return TableUdpCalculationPreview.FromMessage("Tablo bulunamadi.");

            var properties = table.getoObjectProperty();
            if (properties == null || properties.Count == 0)
                return TableUdpCalculationPreview.FromMessage(tableName + ": UDP bulunamadi.");

            ObjectProperty businessProcessLevel = FindProperty(properties, BusinessProcessLevelPropertyNames);
            ObjectProperty bankRelativeValue = FindProperty(properties, BankRelativeValuePropertyNames);

            var missing = new List<string>();
            int assetLevel = 0;
            int businessLevel = 0;

            TableUdpCalculationPreview assetPreview = PreviewAssetValue(table);
            if (assetPreview == null ||
                !TryResolveLeadingIntegerText(assetPreview.CalculatedValue, out assetLevel))
            {
                missing.Add("Veri_Degeri hesaplanamadi");
            }

            if (businessProcessLevel == null)
                missing.Add("Is_Sureci_Seviyesi");

            if (bankRelativeValue == null)
                missing.Add("Banka_Gorece_Degeri");

            if (businessProcessLevel != null && !TryResolveParenthesizedInteger(businessProcessLevel, out businessLevel))
                missing.Add("Is_Sureci_Seviyesi parantez ici sayi");

            if (missing.Count > 0)
                return TableUdpCalculationPreview.FromMessage(
                    BuildPreviewMissingMessage(tableName, "eksik/hatali alanlar", missing));

            var calculation = new BankRelativeValueCalculation
            {
                TableObjectId = table.getoObjectId(),
                TableName = tableName,
                AssetLevel = assetLevel,
                BusinessProcessLevel = businessLevel,
                ResultValue = assetLevel * businessLevel,
                BankRelativeValueProperty = bankRelativeValue
            };

            return TableUdpCalculationPreview.FromCalculation(
                calculation.ToFormulaText(),
                calculation.ResultValue.ToString(CultureInfo.InvariantCulture));
        }

        private static TableUdpCalculationPreview PreviewSecurityClassValue(ModelObject table)
        {
            string tableName = table == null
                ? "(adsiz tablo)"
                : Safe(table.getoName(), "(adsiz tablo)");

            if (table == null)
                return TableUdpCalculationPreview.FromMessage("Tablo bulunamadi.");

            var properties = table.getoObjectProperty();
            if (properties == null || properties.Count == 0)
                return TableUdpCalculationPreview.FromMessage(tableName + ": UDP bulunamadi.");

            ObjectProperty securityClassValue = FindProperty(properties, SecurityClassValuePropertyNames);

            var missing = new List<string>();
            int bankValue = 0;

            TableUdpCalculationPreview bankPreview = PreviewBankRelativeValue(table);
            if (bankPreview == null ||
                !TryResolveLeadingIntegerText(bankPreview.CalculatedValue, out bankValue))
            {
                missing.Add("Banka_Gorece_Degeri hesaplanamadi");
            }

            if (securityClassValue == null)
                missing.Add("Guvenlik_Sinifi_Degeri");

            if (missing.Count > 0)
                return TableUdpCalculationPreview.FromMessage(
                    BuildPreviewMissingMessage(tableName, "eksik/hatali alanlar", missing));

            int trueCount = 0;
            foreach (var column in EnumerateColumns(table))
            {
                var columnProperties = column.getoObjectProperty();
                if (columnProperties == null || columnProperties.Count == 0)
                    continue;

                ObjectProperty personalData = FindProperty(columnProperties, PersonalDataPropertyNames);
                ObjectProperty sensitiveData = FindProperty(columnProperties, SensitiveDataPropertyNames);

                if (IsTrueValue(personalData))
                    trueCount++;

                if (IsTrueValue(sensitiveData))
                    trueCount++;
            }

            long securityValue = bankValue;
            for (int i = 0; i < trueCount; i++)
                securityValue *= 2;

            var calculation = new SecurityClassValueCalculation
            {
                TableObjectId = table.getoObjectId(),
                TableName = tableName,
                BankRelativeValue = bankValue,
                TrueFlagCount = trueCount,
                ResultValue = securityValue,
                SecurityClassValueProperty = securityClassValue
            };

            return TableUdpCalculationPreview.FromCalculation(
                calculation.ToFormulaText(),
                calculation.ResultValue.ToString(CultureInfo.InvariantCulture));
        }

        private static string BuildPreviewMissingMessage(
            string tableName,
            string reason,
            IEnumerable<string> missing)
        {
            return Safe(tableName, "(adsiz tablo)") +
                   ": " +
                   reason +
                   " - " +
                   string.Join(", ", missing ?? new List<string>());
        }

        private static List<TableUdpApprovalChange> BuildApprovalChanges(ModelInfo modelInfo)
        {
            var changes = new List<TableUdpApprovalChange>();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            foreach (var table in EnumerateTables(objects))
            {
                if (table == null)
                    continue;

                string tableName = Safe(table.getoName(), "(adsiz tablo)");
                var properties = table.getoObjectProperty();
                if (properties == null || properties.Count == 0)
                    continue;

                ObjectProperty availability = FindProperty(properties, AvailabilityPropertyNames);
                ObjectProperty integrity = FindProperty(properties, IntegrityPropertyNames);
                ObjectProperty confidentiality = FindProperty(properties, ConfidentialityPropertyNames);
                ObjectProperty assetValue = FindProperty(properties, AssetValuePropertyNames);
                ObjectProperty businessProcessLevel = FindProperty(properties, BusinessProcessLevelPropertyNames);
                ObjectProperty bankRelativeValue = FindProperty(properties, BankRelativeValuePropertyNames);
                ObjectProperty securityClassValue = FindProperty(properties, SecurityClassValuePropertyNames);

                int assetLevel;
                string calculatedAssetValue;
                string assetFormulaText;
                if (!TryCalculateAssetValue(
                        tableName,
                        availability,
                        integrity,
                        confidentiality,
                        out assetLevel,
                        out calculatedAssetValue,
                        out assetFormulaText))
                {
                    continue;
                }

                if (assetValue != null && !IsAssetValueSame(assetValue, assetLevel, calculatedAssetValue))
                {
                    changes.Add(new TableUdpApprovalChange(
                        tableName,
                        "Veri_Degeri",
                        calculatedAssetValue,
                        GetPropertyDisplayValue(assetValue),
                        assetFormulaText));
                }

                int bankRelativeCalculatedValue;
                string bankFormulaText;
                if (!TryCalculateBankRelativeValue(
                        tableName,
                        assetLevel,
                        businessProcessLevel,
                        out bankRelativeCalculatedValue,
                        out bankFormulaText))
                {
                    continue;
                }

                if (bankRelativeValue != null && !IsIntegerValueSame(bankRelativeValue, bankRelativeCalculatedValue))
                {
                    changes.Add(new TableUdpApprovalChange(
                        tableName,
                        "Banka_Gorece_Degeri",
                        bankRelativeCalculatedValue.ToString(CultureInfo.InvariantCulture),
                        GetPropertyDisplayValue(bankRelativeValue),
                        bankFormulaText));
                }

                long securityClassCalculatedValue;
                string securityFormulaText;
                if (!TryCalculateSecurityClassValue(
                        tableName,
                        table,
                        bankRelativeCalculatedValue,
                        out securityClassCalculatedValue,
                        out securityFormulaText))
                {
                    continue;
                }

                if (securityClassValue != null && !IsLongValueSame(securityClassValue, securityClassCalculatedValue))
                {
                    changes.Add(new TableUdpApprovalChange(
                        tableName,
                        "Guvenlik_Sinifi_Degeri",
                        securityClassCalculatedValue.ToString(CultureInfo.InvariantCulture),
                        GetPropertyDisplayValue(securityClassValue),
                        securityFormulaText));
                }
            }

            return changes;
        }

        private static bool TryCalculateAssetValue(
            string tableName,
            ObjectProperty availability,
            ObjectProperty integrity,
            ObjectProperty confidentiality,
            out int resultLevel,
            out string resultValue,
            out string formulaText)
        {
            resultLevel = 0;
            resultValue = string.Empty;
            formulaText = string.Empty;

            int availabilityLevel;
            int integrityLevel;
            int confidentialityLevel;

            if (availability == null ||
                integrity == null ||
                confidentiality == null ||
                !TryResolveLevel(availability, out availabilityLevel) ||
                !TryResolveLevel(integrity, out integrityLevel) ||
                !TryResolveLevel(confidentiality, out confidentialityLevel))
            {
                return false;
            }

            double average = (availabilityLevel + integrityLevel + confidentialityLevel) / 3.0;
            resultLevel = ClampLevel(
                (int)Math.Round(
                    average,
                    MidpointRounding.AwayFromZero));
            resultValue = BuildAssetValue(resultLevel);

            var calculation = new TableUdpSecurityCalculation
            {
                TableName = Safe(tableName, "(adsiz tablo)"),
                AvailabilityLevel = availabilityLevel,
                IntegrityLevel = integrityLevel,
                ConfidentialityLevel = confidentialityLevel,
                Average = average,
                ResultLevel = resultLevel,
                ResultValue = resultValue
            };

            formulaText = calculation.ToFormulaText();
            return true;
        }

        private static bool TryCalculateBankRelativeValue(
            string tableName,
            int assetLevel,
            ObjectProperty businessProcessLevel,
            out int resultValue,
            out string formulaText)
        {
            resultValue = 0;
            formulaText = string.Empty;

            int businessLevel;
            if (assetLevel <= 0 ||
                businessProcessLevel == null ||
                !TryResolveParenthesizedInteger(businessProcessLevel, out businessLevel))
            {
                return false;
            }

            resultValue = assetLevel * businessLevel;
            var calculation = new BankRelativeValueCalculation
            {
                TableName = Safe(tableName, "(adsiz tablo)"),
                AssetLevel = assetLevel,
                BusinessProcessLevel = businessLevel,
                ResultValue = resultValue
            };

            formulaText = calculation.ToFormulaText();
            return true;
        }

        private static bool TryCalculateSecurityClassValue(
            string tableName,
            ModelObject table,
            int bankRelativeValue,
            out long resultValue,
            out string formulaText)
        {
            resultValue = 0;
            formulaText = string.Empty;

            if (table == null || bankRelativeValue <= 0)
                return false;

            int trueCount = 0;
            foreach (var column in EnumerateColumns(table))
            {
                var columnProperties = column.getoObjectProperty();
                if (columnProperties == null || columnProperties.Count == 0)
                    continue;

                ObjectProperty personalData = FindProperty(columnProperties, PersonalDataPropertyNames);
                ObjectProperty sensitiveData = FindProperty(columnProperties, SensitiveDataPropertyNames);

                if (IsTrueValue(personalData))
                    trueCount++;

                if (IsTrueValue(sensitiveData))
                    trueCount++;
            }

            resultValue = bankRelativeValue;
            for (int i = 0; i < trueCount; i++)
                resultValue *= 2;

            var calculation = new SecurityClassValueCalculation
            {
                TableName = Safe(tableName, "(adsiz tablo)"),
                BankRelativeValue = bankRelativeValue,
                TrueFlagCount = trueCount,
                ResultValue = resultValue
            };

            formulaText = calculation.ToFormulaText();
            return true;
        }

        private static string BuildApprovalScriptText(IEnumerable<TableUdpApprovalChange> changes)
        {
            var changeList = changes == null
                ? new List<TableUdpApprovalChange>()
                : changes.ToList();

            if (changeList.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("-- TABLE UDP CALCULATED DIFFERENCES");
            builder.AppendLine("-- TableName          >          UDPName          >          UDPValue");

            foreach (var change in changeList)
            {
                builder.Append("--  ");
                //builder.Append("UPDATE [");
                builder.Append(EscapeSqlIdentifier(change.TableName));

                for (var i = change.TableName.Length; i < 20; i++)
                {
                    builder.Append(" ");
                }
                //builder.Append("] SET [");
                //builder.Append("     >     ");
                builder.Append(EscapeSqlIdentifier(change.UdpName));
                
                for (var i = change.UdpName.Length; i < 20; i++)
                {
                    builder.Append(" ");
                }
                
                //builder.Append("     >     ");
                //builder.Append("] = N'");
                builder.Append(change.UdpValue);
                //builder.Append("'; -- ");
                //builder.Append(EscapeSqlComment(change.TableName));
                //builder.Append(" > ");
                //builder.Append(EscapeSqlComment(change.UdpName));
                //builder.Append(" > ");
                //builder.Append(EscapeSqlComment(change.UdpValue));

                //if (!string.IsNullOrWhiteSpace(change.CurrentValue))
                //{
                //    builder.Append(" | Onceki: ");
                //    builder.Append(EscapeSqlComment(change.CurrentValue));
                //}

                //if (!string.IsNullOrWhiteSpace(change.FormulaText))
                //{
                //    builder.Append(" | ");
                //    builder.Append(EscapeSqlComment(change.FormulaText));
                //}

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string EscapeSqlIdentifier(string value)
        {
            return Safe(value, string.Empty).Replace("]", "]]");
        }

        private static string EscapeSqlString(string value)
        {
            return Safe(value, string.Empty).Replace("'", "''");
        }

        private static string EscapeSqlComment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("*/", "* /");
        }

        public TableUdpSecurityApplyResult Apply(ModelInfo modelInfo)
        {
            var result = new TableUdpSecurityApplyResult();
            var calculations = BuildCalculations(modelInfo, result);

            if (calculations.Count == 0)
                return result;

            if (application == null || persistenceUnit == null)
                throw new InvalidOperationException("Veri degeri guncellemesi icin erwin oturumu hazir degil.");

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

        public static int CountBankRelativeCalculableTables(ModelInfo modelInfo)
        {
            return BuildBankRelativeCalculations(modelInfo, null).Count;
        }

        public TableUdpSecurityApplyResult ApplyBankRelativeValue(ModelInfo modelInfo)
        {
            var result = new TableUdpSecurityApplyResult();
            var calculations = BuildBankRelativeCalculations(modelInfo, result);

            if (calculations.Count == 0)
                return result;

            if (application == null || persistenceUnit == null)
                throw new InvalidOperationException("Banka gorece degeri guncellemesi icin erwin oturumu hazir degil.");

            SCAPI.Session session = null;
            object transaction = null;

            try
            {
                session = application.Sessions.Add();
                session.Open(
                    persistenceUnit,
                    SCAPI.SC_SessionLevel.SCD_SL_M0);

                transaction = session.BeginNamedTransaction("Calculate Bank Relative UDP Values");

                foreach (var calculation in calculations)
                    ApplyBankRelativeCalculation(session, calculation, result);

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

        public static int CountSecurityClassCalculableTables(ModelInfo modelInfo)
        {
            return BuildSecurityClassCalculations(modelInfo, null).Count;
        }

        public TableUdpSecurityApplyResult ApplySecurityClassValue(ModelInfo modelInfo)
        {
            var result = new TableUdpSecurityApplyResult();
            var calculations = BuildSecurityClassCalculations(modelInfo, result);

            if (calculations.Count == 0)
                return result;

            if (application == null || persistenceUnit == null)
                throw new InvalidOperationException("Guvenlik sinifi degeri guncellemesi icin erwin oturumu hazir degil.");

            SCAPI.Session session = null;
            object transaction = null;

            try
            {
                session = application.Sessions.Add();
                session.Open(
                    persistenceUnit,
                    SCAPI.SC_SessionLevel.SCD_SL_M0);

                transaction = session.BeginNamedTransaction("Calculate Security Class UDP Values");

                foreach (var calculation in calculations)
                    ApplySecurityClassCalculation(session, calculation, result);

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
                result.Messages.Add(calculation.TableName + ": Veri_Degeri UDP bulunamadi.");
                return;
            }

            if (IsAssetValueSame(calculation.AssetValueProperty, calculation.ResultLevel, calculation.ResultValue))
            {
                result.UnchangedTables++;
                result.Messages.Add(BuildNoChangeMessage(
                    calculation.TableName,
                    calculation.ToFormulaText()));
                return;
            }

            string previousValue = GetPropertyDisplayValue(calculation.AssetValueProperty);
            string writtenValue;
            if (!TrySetPropertyValue(
                targetProperty,
                calculation.ResultLevel,
                calculation.ResultValue,
                out writtenValue))
            {
                result.FailedTables++;
                result.Messages.Add(calculation.TableName + ": Veri_Degeri yazilamadi.");
                return;
            }

            calculation.AssetValueProperty.setoPropertyValue(writtenValue);
            calculation.AssetValueProperty.setoPropertyFormatAsString(calculation.ResultValue);
            result.UpdatedTables++;
            result.Messages.Add(BuildUpdatedMessage(
                calculation.TableName,
                calculation.ToFormulaText(),
                previousValue,
                calculation.ResultValue));
        }

        private void ApplyBankRelativeCalculation(
            SCAPI.Session session,
            BankRelativeValueCalculation calculation,
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
                BankRelativeValuePropertyNames);

            if (targetProperty == null)
            {
                result.SkippedTables++;
                result.Messages.Add(calculation.TableName + ": Banka_Gorece_Degeri UDP bulunamadi.");
                return;
            }

            if (IsIntegerValueSame(calculation.BankRelativeValueProperty, calculation.ResultValue))
            {
                result.UnchangedTables++;
                result.Messages.Add(BuildNoChangeMessage(
                    calculation.TableName,
                    calculation.ToFormulaText()));
                return;
            }

            string previousValue = GetPropertyDisplayValue(calculation.BankRelativeValueProperty);
            string writtenValue;
            if (!TrySetPropertyValue(
                targetProperty,
                calculation.ResultValue,
                out writtenValue))
            {
                result.FailedTables++;
                result.Messages.Add(calculation.TableName + ": Banka_Gorece_Degeri yazilamadi.");
                return;
            }

            calculation.BankRelativeValueProperty.setoPropertyValue(writtenValue);
            calculation.BankRelativeValueProperty.setoPropertyFormatAsString(writtenValue);
            result.UpdatedTables++;
            result.Messages.Add(BuildUpdatedMessage(
                calculation.TableName,
                calculation.ToFormulaText(),
                previousValue,
                writtenValue));
        }

        private void ApplySecurityClassCalculation(
            SCAPI.Session session,
            SecurityClassValueCalculation calculation,
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
                SecurityClassValuePropertyNames);

            if (targetProperty == null)
            {
                result.SkippedTables++;
                result.Messages.Add(calculation.TableName + ": Guvenlik_Sinifi_Degeri UDP bulunamadi.");
                return;
            }

            if (IsLongValueSame(calculation.SecurityClassValueProperty, calculation.ResultValue))
            {
                result.UnchangedTables++;
                result.Messages.Add(BuildNoChangeMessage(
                    calculation.TableName,
                    calculation.ToFormulaText()));
                return;
            }

            string previousValue = GetPropertyDisplayValue(calculation.SecurityClassValueProperty);
            string writtenValue;
            if (!TrySetPropertyValue(
                targetProperty,
                calculation.ResultValue,
                out writtenValue))
            {
                result.FailedTables++;
                result.Messages.Add(calculation.TableName + ": Guvenlik_Sinifi_Degeri yazilamadi.");
                return;
            }

            calculation.SecurityClassValueProperty.setoPropertyValue(writtenValue);
            calculation.SecurityClassValueProperty.setoPropertyFormatAsString(writtenValue);
            result.UpdatedTables++;
            result.Messages.Add(BuildUpdatedMessage(
                calculation.TableName,
                calculation.ToFormulaText(),
                previousValue,
                writtenValue));
        }

        private SCAPI.ModelObject FindTargetTable(
            SCAPI.Session session,
            TableUdpCalculationBase calculation)
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

        private static bool TrySetPropertyValue(
            SCAPI.ModelProperty property,
            int resultValue,
            out string writtenValue)
        {
            string textValue = resultValue.ToString(CultureInfo.InvariantCulture);
            object[] attempts =
            {
                resultValue,
                textValue
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

        private static bool TrySetPropertyValue(
            SCAPI.ModelProperty property,
            long resultValue,
            out string writtenValue)
        {
            string textValue = resultValue.ToString(CultureInfo.InvariantCulture);
            object[] attempts =
            {
                resultValue,
                textValue
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

        private static bool IsAssetValueSame(
            ObjectProperty property,
            int resultLevel,
            string resultValue)
        {
            if (property == null)
                return false;

            int currentLevel;
            if (TryResolveLevel(property, out currentLevel) && currentLevel == resultLevel)
                return true;

            return AreDisplayValuesSame(GetPropertyDisplayValue(property), resultValue);
        }

        private static bool IsIntegerValueSame(ObjectProperty property, int resultValue)
        {
            if (property == null)
                return false;

            int currentValue;
            if (TryResolveLeadingInteger(property, out currentValue) && currentValue == resultValue)
                return true;

            return AreDisplayValuesSame(
                GetPropertyDisplayValue(property),
                resultValue.ToString(CultureInfo.InvariantCulture));
        }

        private static bool IsLongValueSame(ObjectProperty property, long resultValue)
        {
            if (property == null)
                return false;

            long currentValue;
            if (TryResolveLeadingLong(property, out currentValue) && currentValue == resultValue)
                return true;

            return AreDisplayValuesSame(
                GetPropertyDisplayValue(property),
                resultValue.ToString(CultureInfo.InvariantCulture));
        }

        private static bool AreDisplayValuesSame(string left, string right)
        {
            return string.Equals(
                NormalizeValue(left),
                NormalizeValue(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPropertyDisplayValue(ObjectProperty property)
        {
            if (property == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(property.getoPropertyFormatAsString()))
                return property.getoPropertyFormatAsString();

            return property.getoPropertyValue() ?? string.Empty;
        }

        private static string BuildUpdatedMessage(
            string tableName,
            string formulaText,
            string previousValue,
            string writtenValue)
        {
            return Safe(tableName, "(adsiz tablo)") +
                   ": " +
                   formulaText +
                   " (guncellendi: " +
                   Safe(previousValue, "-") +
                   " -> " +
                   Safe(writtenValue, "-") +
                   ")";
        }

        private static string BuildNoChangeMessage(string tableName, string formulaText)
        {
            return Safe(tableName, "(adsiz tablo)") +
                   ": " +
                   formulaText +
                   " (degismedi)";
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
                    missing.Add("Erisilebilirlik");

                if (integrity == null)
                    missing.Add("Butunluk");

                if (confidentiality == null)
                    missing.Add("Gizlilik_Seviyesi");

                if (assetValue == null)
                    missing.Add("Veri_Degeri");

                if (availability != null && !TryResolveLevel(availability, out availabilityLevel))
                    missing.Add("Erisilebilirlik seviyesi");

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

        private static List<BankRelativeValueCalculation> BuildBankRelativeCalculations(
            ModelInfo modelInfo,
            TableUdpSecurityApplyResult result)
        {
            var calculations = new List<BankRelativeValueCalculation>();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            foreach (var table in EnumerateTables(objects))
            {
                var properties = table.getoObjectProperty();
                if (properties == null || properties.Count == 0)
                    continue;

                ObjectProperty assetValue = FindProperty(properties, AssetValuePropertyNames);
                ObjectProperty businessProcessLevel = FindProperty(properties, BusinessProcessLevelPropertyNames);
                ObjectProperty bankRelativeValue = FindProperty(properties, BankRelativeValuePropertyNames);

                var missing = new List<string>();
                int assetLevel = 0;
                int businessLevel = 0;

                if (assetValue == null)
                    missing.Add("Veri_Degeri");

                if (businessProcessLevel == null)
                    missing.Add("Is_Sureci_Seviyesi");

                if (bankRelativeValue == null)
                    missing.Add("Banka_Gorece_Degeri");

                if (assetValue != null && !TryResolveLeadingInteger(assetValue, out assetLevel))
                    missing.Add("Veri_Degeri sayi ile baslamiyor");

                if (businessProcessLevel != null && !TryResolveParenthesizedInteger(businessProcessLevel, out businessLevel))
                    missing.Add("Is_Sureci_Seviyesi parantez ici sayi");

                if (missing.Count > 0)
                {
                    if (result != null)
                    {
                        result.SkippedTables++;
                        result.Messages.Add(
                            Safe(table.getoName(), "(adsiz tablo)") +
                            ": eksik/hatali alanlar - " +
                            string.Join(", ", missing));
                    }

                    continue;
                }

                calculations.Add(new BankRelativeValueCalculation
                {
                    TableObjectId = table.getoObjectId(),
                    TableName = Safe(table.getoName(), "(adsiz tablo)"),
                    AssetLevel = assetLevel,
                    BusinessProcessLevel = businessLevel,
                    ResultValue = assetLevel * businessLevel,
                    BankRelativeValueProperty = bankRelativeValue
                });
            }

            return calculations;
        }

        private static List<SecurityClassValueCalculation> BuildSecurityClassCalculations(
            ModelInfo modelInfo,
            TableUdpSecurityApplyResult result)
        {
            var calculations = new List<SecurityClassValueCalculation>();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            foreach (var table in EnumerateTables(objects))
            {
                var properties = table.getoObjectProperty();
                if (properties == null || properties.Count == 0)
                    continue;

                ObjectProperty bankRelativeValue = FindProperty(properties, BankRelativeValuePropertyNames);
                ObjectProperty securityClassValue = FindProperty(properties, SecurityClassValuePropertyNames);

                var missing = new List<string>();
                int bankValue = 0;

                if (bankRelativeValue == null)
                    missing.Add("Banka_Gorece_Degeri");

                if (securityClassValue == null)
                    missing.Add("Guvenlik_Sinifi_Degeri");

                if (bankRelativeValue != null && !TryResolveLeadingInteger(bankRelativeValue, out bankValue))
                    missing.Add("Banka_Gorece_Degeri sayisi");

                if (missing.Count > 0)
                {
                    if (result != null)
                    {
                        result.SkippedTables++;
                        result.Messages.Add(
                            Safe(table.getoName(), "(adsiz tablo)") +
                            ": eksik/hatali alanlar - " +
                            string.Join(", ", missing));
                    }

                    continue;
                }

                int trueCount = 0;
                foreach (var column in EnumerateColumns(table))
                {
                    var columnProperties = column.getoObjectProperty();
                    if (columnProperties == null || columnProperties.Count == 0)
                        continue;

                    ObjectProperty personalData = FindProperty(columnProperties, PersonalDataPropertyNames);
                    ObjectProperty sensitiveData = FindProperty(columnProperties, SensitiveDataPropertyNames);

                    if (IsTrueValue(personalData))
                        trueCount++;

                    if (IsTrueValue(sensitiveData))
                        trueCount++;
                }

                long securityValue = bankValue;
                for (int i = 0; i < trueCount; i++)
                    securityValue *= 2;

                calculations.Add(new SecurityClassValueCalculation
                {
                    TableObjectId = table.getoObjectId(),
                    TableName = Safe(table.getoName(), "(adsiz tablo)"),
                    BankRelativeValue = bankValue,
                    TrueFlagCount = trueCount,
                    ResultValue = securityValue,
                    SecurityClassValueProperty = securityClassValue
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

        private static ModelObject FindModelTable(
            ModelInfo modelInfo,
            string tableObjectId,
            string tableName)
        {
            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            foreach (var table in EnumerateTables(objects))
            {
                if (table == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(tableObjectId) &&
                    string.Equals(
                        table.getoObjectId(),
                        tableObjectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return table;
                }

                if (!string.IsNullOrWhiteSpace(tableName) &&
                    string.Equals(
                        table.getoName(),
                        tableName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return table;
                }
            }

            return null;
        }

        private static IEnumerable<ModelObject> EnumerateColumns(ModelObject table)
        {
            if (table == null)
                yield break;

            var children = table.getoModelObject();
            if (children == null)
                yield break;

            foreach (var child in children)
            {
                if (child == null)
                    continue;

                if (string.Equals(child.getoClassName(), "Attribute", StringComparison.OrdinalIgnoreCase))
                    yield return child;

                foreach (var nestedChild in EnumerateColumns(child))
                    yield return nestedChild;
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

        private static bool TryResolveLeadingInteger(ObjectProperty property, out int level)
        {
            if (TryResolveLeadingIntegerText(property.getoPropertyFormatAsString(), out level))
                return true;

            if (TryResolveLeadingIntegerText(property.getoPropertyValue(), out level))
                return true;

            level = 0;
            return false;
        }

        private static bool TryResolveLeadingIntegerText(string value, out int level)
        {
            level = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            int index = 0;

            while (index < trimmed.Length && char.IsDigit(trimmed[index]))
                index++;

            if (index == 0)
                return false;

            return int.TryParse(
                trimmed.Substring(0, index),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out level);
        }

        private static bool TryResolveLeadingLong(ObjectProperty property, out long value)
        {
            if (TryResolveLeadingLongText(property.getoPropertyFormatAsString(), out value))
                return true;

            if (TryResolveLeadingLongText(property.getoPropertyValue(), out value))
                return true;

            value = 0;
            return false;
        }

        private static bool TryResolveLeadingLongText(string value, out long number)
        {
            number = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            int index = 0;

            while (index < trimmed.Length && char.IsDigit(trimmed[index]))
                index++;

            if (index == 0)
                return false;

            return long.TryParse(
                trimmed.Substring(0, index),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out number);
        }

        private static bool TryResolveParenthesizedInteger(ObjectProperty property, out int level)
        {
            if (TryResolveParenthesizedIntegerText(property.getoPropertyFormatAsString(), out level))
                return true;

            if (TryResolveParenthesizedIntegerText(property.getoPropertyValue(), out level))
                return true;

            level = 0;
            return false;
        }

        private static bool TryResolveParenthesizedIntegerText(string value, out int level)
        {
            level = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            int openIndex = value.LastIndexOf('(');
            int closeIndex = value.LastIndexOf(')');

            if (openIndex < 0 || closeIndex <= openIndex + 1)
                return false;

            string numberText = value.Substring(
                openIndex + 1,
                closeIndex - openIndex - 1).Trim();

            return int.TryParse(
                numberText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out level);
        }

        private static bool IsTrueValue(ObjectProperty property)
        {
            if (property == null)
                return false;

            return IsTrueText(property.getoPropertyFormatAsString()) ||
                   IsTrueText(property.getoPropertyValue());
        }

        private static bool IsTrueText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = NormalizeValue(value);

            return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "evet", StringComparison.OrdinalIgnoreCase);
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

        public int UnchangedTables { get; set; }

        public int SkippedTables { get; set; }

        public int FailedTables { get; set; }

        public List<string> Messages { get; private set; }

        public string ToSummary()
        {
            return UpdatedTables + " tablo guncellendi, " +
                   UnchangedTables + " tablo degismedi, " +
                   SkippedTables + " tablo atlandi, " +
                   FailedTables + " hata";
        }
    }

    internal sealed class TableUdpApprovalScriptResult
    {
        public TableUdpApprovalScriptResult(
            IEnumerable<TableUdpApprovalChange> changes,
            string scriptText)
        {
            Changes = changes == null
                ? new List<TableUdpApprovalChange>()
                : new List<TableUdpApprovalChange>(changes);
            ScriptText = scriptText ?? string.Empty;
        }

        public List<TableUdpApprovalChange> Changes { get; private set; }

        public string ScriptText { get; private set; }

        public int ChangeCount
        {
            get { return Changes.Count; }
        }
    }

    internal sealed class TableUdpApprovalChange
    {
        public TableUdpApprovalChange(
            string tableName,
            string udpName,
            string udpValue,
            string currentValue,
            string formulaText)
        {
            TableName = tableName ?? string.Empty;
            UdpName = udpName ?? string.Empty;
            UdpValue = udpValue ?? string.Empty;
            CurrentValue = currentValue ?? string.Empty;
            FormulaText = formulaText ?? string.Empty;
        }

        public string TableName { get; private set; }

        public string UdpName { get; private set; }

        public string UdpValue { get; private set; }

        public string CurrentValue { get; private set; }

        public string FormulaText { get; private set; }
    }

    internal sealed class TableUdpCalculationPreview
    {
        private TableUdpCalculationPreview()
        {
        }

        public string FormulaText { get; private set; }

        public string CalculatedValue { get; private set; }

        public string Message { get; private set; }

        public static TableUdpCalculationPreview FromCalculation(
            string formulaText,
            string calculatedValue)
        {
            return new TableUdpCalculationPreview
            {
                FormulaText = formulaText ?? string.Empty,
                CalculatedValue = calculatedValue ?? string.Empty
            };
        }

        public static TableUdpCalculationPreview FromMessage(string message)
        {
            return new TableUdpCalculationPreview
            {
                Message = message ?? string.Empty
            };
        }
    }

    internal sealed class TableUdpStartupApplyResult
    {
        public TableUdpStartupApplyResult(DateTime startedAt)
        {
            StartedAt = startedAt;
            Operations = new List<TableUdpStartupOperationResult>();
        }

        public DateTime StartedAt { get; private set; }

        public DateTime? CompletedAt { get; set; }

        public List<TableUdpStartupOperationResult> Operations { get; private set; }

        public int TotalUpdatedTables
        {
            get { return Operations.Sum(operation => operation.UpdatedTables); }
        }

        public int TotalSkippedTables
        {
            get { return Operations.Sum(operation => operation.SkippedTables); }
        }

        public int TotalUnchangedTables
        {
            get { return Operations.Sum(operation => operation.UnchangedTables); }
        }

        public int TotalFailedTables
        {
            get { return Operations.Sum(operation => operation.FailedTables); }
        }

        public bool HasErrors
        {
            get
            {
                return Operations.Any(operation =>
                    !operation.IsSuccess ||
                    operation.FailedTables > 0);
            }
        }

        public void AddOperation(
            string name,
            Func<TableUdpSecurityApplyResult> operation)
        {
            try
            {
                TableUdpSecurityApplyResult result = operation == null
                    ? null
                    : operation();

                Operations.Add(TableUdpStartupOperationResult.FromResult(name, result));
            }
            catch (Exception ex)
            {
                Operations.Add(TableUdpStartupOperationResult.FromException(name, ex));
            }
        }

        public string ToSummaryLine()
        {
            if (Operations.Count == 0)
                return "Tablo UDP islemleri calismadi.";

            return TotalUpdatedTables + " guncellendi, " +
                   TotalUnchangedTables + " degismedi, " +
                   TotalSkippedTables + " atlandi, " +
                   TotalFailedTables + " hata";
        }
    }

    internal sealed class TableUdpStartupOperationResult
    {
        private TableUdpStartupOperationResult()
        {
        }

        public string Name { get; private set; }

        public int UpdatedTables { get; private set; }

        public int UnchangedTables { get; private set; }

        public int SkippedTables { get; private set; }

        public int FailedTables { get; private set; }

        public string ErrorMessage { get; private set; }

        public List<string> Messages { get; private set; }

        public bool IsSuccess
        {
            get { return string.IsNullOrWhiteSpace(ErrorMessage); }
        }

        public string SummaryText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ErrorMessage))
                    return ErrorMessage;

                return UpdatedTables + " guncellendi, " +
                       UnchangedTables + " degismedi, " +
                       SkippedTables + " atlandi, " +
                       FailedTables + " hata";
            }
        }

        public string DetailText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ErrorMessage))
                    return ErrorMessage;

                if (Messages == null || Messages.Count == 0)
                    return SummaryText;

                return SummaryText + Environment.NewLine +
                       string.Join(Environment.NewLine, Messages.Take(8));
            }
        }

        public static TableUdpStartupOperationResult FromResult(
            string name,
            TableUdpSecurityApplyResult result)
        {
            return new TableUdpStartupOperationResult
            {
                Name = name ?? string.Empty,
                UpdatedTables = result == null ? 0 : result.UpdatedTables,
                UnchangedTables = result == null ? 0 : result.UnchangedTables,
                SkippedTables = result == null ? 0 : result.SkippedTables,
                FailedTables = result == null ? 0 : result.FailedTables,
                Messages = result == null
                    ? new List<string>()
                    : new List<string>(result.Messages ?? new List<string>())
            };
        }

        public static TableUdpStartupOperationResult FromException(
            string name,
            Exception ex)
        {
            return new TableUdpStartupOperationResult
            {
                Name = name ?? string.Empty,
                ErrorMessage = ex == null ? "Bilinmeyen hata" : ex.Message,
                FailedTables = 1,
                Messages = new List<string>()
            };
        }
    }

    internal abstract class TableUdpCalculationBase
    {
        public string TableObjectId { get; set; }

        public string TableName { get; set; }
    }

    internal sealed class TableUdpSecurityCalculation : TableUdpCalculationBase
    {

        public int AvailabilityLevel { get; set; }

        public int IntegrityLevel { get; set; }

        public int ConfidentialityLevel { get; set; }

        public double Average { get; set; }

        public int ResultLevel { get; set; }

        public string ResultValue { get; set; }

        public ObjectProperty AssetValueProperty { get; set; }

        public string ToFormulaText()
        {
            return "Erisilebilirlik(" +
                   AvailabilityLevel +
                   ") & Butunluk(" +
                   IntegrityLevel +
                   ") & Gizlilik_Seviyesi(" +
                   ConfidentialityLevel +
                   ") = Veri_Degeri(" +
                   (string.IsNullOrWhiteSpace(ResultValue) ? "-" : ResultValue) +
                   ")";
        }
    }

    internal sealed class BankRelativeValueCalculation : TableUdpCalculationBase
    {
        public int AssetLevel { get; set; }

        public int BusinessProcessLevel { get; set; }

        public int ResultValue { get; set; }

        public ObjectProperty BankRelativeValueProperty { get; set; }

        public string ToFormulaText()
        {
            return "Veri_Degeri(" +
                   AssetLevel +
                   ") & Is_Sureci_Seviyesi(" +
                   BusinessProcessLevel +
                   ") = Banka_Gorece_Degeri(" +
                   ResultValue +
                   ")";
        }
    }

    internal sealed class SecurityClassValueCalculation : TableUdpCalculationBase
    {
        public int BankRelativeValue { get; set; }

        public int TrueFlagCount { get; set; }

        public long ResultValue { get; set; }

        public ObjectProperty SecurityClassValueProperty { get; set; }

        public string ToFormulaText()
        {
            return "Banka_Gorece_Degeri(" +
                   BankRelativeValue +
                   ") & Kisisel/Hassas Veri Sayisi(" +
                   TrueFlagCount +
                   ") = Guvenlik_Sinifi_Degeri(" +
                   ResultValue +
                   ")";
        }
    }
}
