using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Veloxap.AddIn.Erwin.Models;
using Veloxap.AddIn.Erwin.Services;

namespace Veloxap.AddIn.Erwin.ViewModels
{
    public class ModelInfoViewModel : INotifyPropertyChanged
    {
        private const int MinimumSearchLength = 3;
        private static readonly NaturalStringComparer NaturalComparer = new NaturalStringComparer();
        private readonly ModelInfo modelInfo;
        private ModelDisplayItem selectedModelItem;
        private string statusMessage;
        private bool isLocked;
        private string lockStatusText;
        private string lockDetailText;
        private string versionOwnerText;
        private string approvalStatusText;
        private string catalogOverviewMessage;
        private List<CatalogLockInfo> catalogLocks;
        private CatalogApprovalStatus catalogApprovalStatus;
        private bool canUnlockCatalog;
        private bool isUnlockingCatalog;
        private bool showApprovalStatus;
        private string unlockCatalogMessage;

        public ObservableCollection<ModelDisplayItem> ModelItems { get; }

        public ObservableCollection<PropertyDisplayItem> Properties { get; }

        public ObservableCollection<ApprovalStepDisplayItem> ApprovalSteps { get; }

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

        public bool IsLocked
        {
            get
            {
                return isLocked;
            }
            private set
            {
                if (isLocked == value)
                    return;

                isLocked = value;
                OnPropertyChanged(nameof(IsLocked));
            }
        }

        public string LockStatusText
        {
            get
            {
                return lockStatusText;
            }
            private set
            {
                if (lockStatusText == value)
                    return;

                lockStatusText = value;
                OnPropertyChanged(nameof(LockStatusText));
            }
        }

        public string LockDetailText
        {
            get
            {
                return lockDetailText;
            }
            private set
            {
                if (lockDetailText == value)
                    return;

                lockDetailText = value;
                OnPropertyChanged(nameof(LockDetailText));
            }
        }

        public string VersionOwnerText
        {
            get
            {
                return versionOwnerText;
            }
            private set
            {
                if (versionOwnerText == value)
                    return;

                versionOwnerText = value;
                OnPropertyChanged(nameof(VersionOwnerText));
            }
        }

        public string ApprovalStatusText
        {
            get
            {
                return approvalStatusText;
            }
            private set
            {
                if (approvalStatusText == value)
                    return;

                approvalStatusText = value;
                OnPropertyChanged(nameof(ApprovalStatusText));
            }
        }

        public string CatalogOverviewMessage
        {
            get
            {
                return catalogOverviewMessage;
            }
            private set
            {
                if (catalogOverviewMessage == value)
                    return;

                catalogOverviewMessage = value;
                OnPropertyChanged(nameof(CatalogOverviewMessage));
            }
        }

        public bool CanUnlockCatalog
        {
            get
            {
                return canUnlockCatalog;
            }
            private set
            {
                if (canUnlockCatalog == value)
                    return;

                canUnlockCatalog = value;
                OnPropertyChanged(nameof(CanUnlockCatalog));
            }
        }

        public bool ShowApprovalStatus
        {
            get
            {
                return showApprovalStatus;
            }
            private set
            {
                if (showApprovalStatus == value)
                    return;

                showApprovalStatus = value;
                OnPropertyChanged(nameof(ShowApprovalStatus));
            }
        }

        public bool IsUnlockingCatalog
        {
            get
            {
                return isUnlockingCatalog;
            }
            private set
            {
                if (isUnlockingCatalog == value)
                    return;

                isUnlockingCatalog = value;
                OnPropertyChanged(nameof(IsUnlockingCatalog));
                UpdateCanUnlockCatalog();
            }
        }

        public string UnlockCatalogMessage
        {
            get
            {
                return unlockCatalogMessage;
            }
            private set
            {
                if (unlockCatalogMessage == value)
                    return;

                unlockCatalogMessage = value;
                OnPropertyChanged(nameof(UnlockCatalogMessage));
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
            ApprovalSteps = new ObservableCollection<ApprovalStepDisplayItem>();
            catalogLocks = new List<CatalogLockInfo>();
            StatusMessage = "Model bilgisi bulunamadi.";
            LockStatusText = "Kontrol edilmedi";
            LockDetailText = "Lock servisi henuz sorgulanmadi.";
            VersionOwnerText = ResolveModelOwnerText();
            ApprovalStatusText = "Kontrol edilmedi";
            CatalogOverviewMessage = string.Empty;
            UnlockCatalogMessage = string.Empty;
            ShowApprovalStatus = true;

            LoadModel();
        }

        internal async Task LoadCatalogOverviewAsync(
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            string fallbackOwner = ResolveModelOwnerText();
            VersionOwnerText = "Kontrol ediliyor...";
            ApprovalSteps.Clear();
            ApprovalStatusText = "Kontrol ediliyor...";
            LockStatusText = "Kontrol ediliyor";
            LockDetailText = "Lock servisi sorgulaniyor...";
            CatalogOverviewMessage = string.Empty;
            UnlockCatalogMessage = string.Empty;
            ShowApprovalStatus = true;
            catalogLocks.Clear();
            catalogApprovalStatus = null;
            CanUnlockCatalog = false;

            if (ruleService == null)
            {
                LockStatusText = "Bilinmiyor";
                LockDetailText = "Servis baglantisi hazir degil.";
                VersionOwnerText = string.IsNullOrWhiteSpace(fallbackOwner) ? "Servis baglantisi hazir degil." : fallbackOwner;
                ApprovalStatusText = "Servis baglantisi hazir degil.";
                UpdateCanUnlockCatalog();
                return;
            }

            if (string.IsNullOrWhiteSpace(catalogName) || string.IsNullOrWhiteSpace(catalogLongId))
            {
                LockStatusText = "Bilinmiyor";
                LockDetailText = "cName veya cLongId okunamadi.";
                VersionOwnerText = string.IsNullOrWhiteSpace(fallbackOwner) ? "cLongId okunamadi." : fallbackOwner;
                ApprovalStatusText = "cName veya cLongId okunamadi.";
                UpdateCanUnlockCatalog();
                return;
            }

            await LoadVersionOwnerAsync(ruleService, catalogName, catalogLongId, fallbackOwner);
            bool shouldLoadApprovalStatus = await LoadLockStatusAsync(ruleService, catalogName, catalogLongId);
            if (shouldLoadApprovalStatus)
                await LoadApprovalStatusAsync(ruleService, catalogName, catalogLongId);
        }

        private async Task LoadVersionOwnerAsync(
            RuleService ruleService,
            string catalogName,
            string catalogLongId,
            string fallbackOwner)
        {
            try
            {
                string versionName = ResolveVersionName(catalogName);
                string versionNumber = ResolveVersionNumber(catalogName);

                CatalogVersionOwnerInfo ownerInfo = await ruleService.GetCatalogVersionOwnerAsync(
                    RuleApiSettings.GetMartCatalogsUrl(),
                    RuleApiSettings.GetMartCatalogVersionsUrlTemplate(),
                    RuleApiSettings.GetAuthUsername(),
                    catalogLongId,
                    versionName,
                    versionNumber);

                VersionOwnerText = ownerInfo == null || string.IsNullOrWhiteSpace(ownerInfo.CreatedBy)
                    ? "Bulunamadi"
                    : ownerInfo.CreatedBy;
            }
            catch (Exception ex)
            {
                VersionOwnerText = string.IsNullOrWhiteSpace(fallbackOwner) || string.Equals(fallbackOwner, "Bulunamadi", StringComparison.OrdinalIgnoreCase)
                    ? "Okunamadi: " + ex.Message
                    : fallbackOwner;
            }
        }

        private async Task<bool> LoadLockStatusAsync(
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            try
            {
                CatalogLocksResult result = await ruleService.GetCatalogLocksAsync(
                    RuleApiSettings.GetCatalogLocksUrl(),
                    catalogName,
                    catalogLongId);

                if (IsCatalogCouldNotBeResolved(result))
                {
                    catalogLocks = new List<CatalogLockInfo>();
                    IsLocked = false;
                    LockStatusText = "Bilinmiyor";
                    LockDetailText = result.Message;
                    ApprovalSteps.Clear();
                    ApprovalStatusText = string.Empty;
                    catalogApprovalStatus = null;
                    ShowApprovalStatus = false;
                    UpdateCanUnlockCatalog();
                    return false;
                }

                catalogLocks = result == null ? new List<CatalogLockInfo>() : result.Locks;
                IsLocked = catalogLocks.Count > 0;
                LockStatusText = IsLocked ? BuildLockStatusText(catalogLocks) : "Lock yok";
                LockDetailText = IsLocked
                    ? BuildLockDetailText(catalogLocks, VersionOwnerText)
                    : BuildNoLockDetailText(result);
            }
            catch (Exception ex)
            {
                catalogLocks = new List<CatalogLockInfo>();
                IsLocked = false;
                LockStatusText = "Bilinmiyor";
                LockDetailText = "Lock bilgisi okunamadi: " + ex.Message;
            }

            UpdateCanUnlockCatalog();
            return true;
        }

        private async Task LoadApprovalStatusAsync(
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            try
            {
                CatalogApprovalStatus approvalStatus = await ruleService.GetApprovalStatusByCatalogAsync(
                    RuleApiSettings.GetApprovalStatusByCatalogUrl(),
                    catalogName,
                    catalogLongId);
                catalogApprovalStatus = approvalStatus;

                ApprovalSteps.Clear();

                foreach (CatalogApprovalStep step in approvalStatus == null
                    ? Enumerable.Empty<CatalogApprovalStep>()
                    : approvalStatus.Steps)
                {
                    ApprovalSteps.Add(new ApprovalStepDisplayItem
                    {
                        StepText = BuildApprovalStepText(step),
                        StatusText = step == null ? string.Empty : step.Status
                    });
                }

                ApprovalStatusText = BuildApprovalStatusText(approvalStatus);
            }
            catch (Exception ex)
            {
                catalogApprovalStatus = null;
                ApprovalSteps.Clear();
                ApprovalStatusText = "Onay durumu okunamadi: " + ex.Message;
            }

            UpdateCanUnlockCatalog();
        }

        internal async Task UnlockCatalogAsync(
            RuleService ruleService,
            string catalogName,
            string catalogLongId)
        {
            if (ruleService == null)
            {
                UnlockCatalogMessage = "Servis baglantisi hazir degil.";
                return;
            }

            UpdateCanUnlockCatalog();

            if (!CanUnlockCatalog)
                return;

            string lockId = ResolveUnlockLockId();
            IsUnlockingCatalog = true;
            UnlockCatalogMessage = "Lock kaldiriliyor...";

            try
            {
                string unlockMessage = await ruleService.UnlockCatalogAsync(
                    RuleApiSettings.GetCatalogUnlockUrl(),
                    catalogName,
                    catalogLongId,
                    lockId);

                UnlockCatalogMessage = string.IsNullOrWhiteSpace(unlockMessage)
                    ? "Lock kaldirildi."
                    : unlockMessage;
                await LoadLockStatusAsync(ruleService, catalogName, catalogLongId);
            }
            catch (Exception ex)
            {
                UnlockCatalogMessage = "Lock kaldirilamadi: " + ex.Message;
            }
            finally
            {
                IsUnlockingCatalog = false;
                UpdateCanUnlockCatalog();
            }
        }

        private void UpdateCanUnlockCatalog()
        {
            CanUnlockCatalog =
                !IsUnlockingCatalog &&
                IsLocked &&
                !string.IsNullOrWhiteSpace(ResolveUnlockLockId()) &&
                IsCurrentUserVersionOwner() &&
                IsApprovalNotStarted(catalogApprovalStatus);
        }

        private string ResolveUnlockLockId()
        {
            CatalogLockInfo lockInfo = (catalogLocks ?? new List<CatalogLockInfo>())
                .FirstOrDefault(item => item != null && !string.IsNullOrWhiteSpace(item.LockId));

            return lockInfo == null ? string.Empty : lockInfo.LockId;
        }

        private bool IsCurrentUserVersionOwner()
        {
            return NamesMatch(VersionOwnerText, RuleApiSettings.GetAuthUsername());
        }

        private static bool IsApprovalNotStarted(CatalogApprovalStatus approvalStatus)
        {
            if (approvalStatus == null)
                return false;

            if (approvalStatus.ApprovalProcessStarted.HasValue)
                return !approvalStatus.ApprovalProcessStarted.Value;

            bool hasApprovalSignal =
                !string.IsNullOrWhiteSpace(approvalStatus.Status) ||
                !string.IsNullOrWhiteSpace(approvalStatus.Message) ||
                !string.IsNullOrWhiteSpace(approvalStatus.StepText) ||
                !string.IsNullOrWhiteSpace(approvalStatus.CurrentApprovalOrder) ||
                (approvalStatus.Steps != null && approvalStatus.Steps.Count > 0);

            if (!hasApprovalSignal)
                return true;

            string statusText = string.Join(" ", new[] { approvalStatus.Status, approvalStatus.Message, approvalStatus.StepText });
            if (ContainsAny(statusText, "baslamadi", "başlamadı", "not started", "notstarted"))
                return true;

            if (ContainsAny(statusText, "basladi", "başladı", "pending", "waiting", "approved", "rejected"))
                return false;

            return false;
        }

        private static bool NamesMatch(string left, string right)
        {
            string normalizedLeft = NormalizeUserName(left);
            string normalizedRight = NormalizeUserName(right);

            return !string.IsNullOrWhiteSpace(normalizedLeft) &&
                   !string.IsNullOrWhiteSpace(normalizedRight) &&
                   string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeUserName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            int domainIndex = normalized.LastIndexOf('\\');
            if (domainIndex >= 0 && domainIndex < normalized.Length - 1)
                normalized = normalized.Substring(domainIndex + 1);

            int mailIndex = normalized.IndexOf('@');
            if (mailIndex > 0)
                normalized = normalized.Substring(0, mailIndex);

            return normalized.Trim();
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null)
                return false;

            return tokens.Any(token =>
                !string.IsNullOrWhiteSpace(token) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildLockDetailText(
            IEnumerable<CatalogLockInfo> locks,
            string versionOwnerText)
        {
            string owner = string.IsNullOrWhiteSpace(versionOwnerText)
                ? "versiyon sahibi bilinmiyor"
                : versionOwnerText;

            var lockTexts = (locks ?? Enumerable.Empty<CatalogLockInfo>())
                .Where(lockInfo => lockInfo != null)
                .Select(lockInfo =>
                {
                    string lockType = string.IsNullOrWhiteSpace(lockInfo.Lock)
                        ? "lock"
                        : lockInfo.Lock;

                    string time = string.IsNullOrWhiteSpace(lockInfo.Time)
                        ? string.Empty
                        : " - " + lockInfo.Time;

                    return owner + " / " + lockType + time;
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            return lockTexts.Count == 0
                ? "Aktif catalog lock kaydi var."
                : string.Join("; ", lockTexts);
        }

        private static string BuildNoLockDetailText(CatalogLocksResult result)
        {
            if (result != null &&
                !result.Success &&
                !string.IsNullOrWhiteSpace(result.Message))
            {
                return result.Message;
            }

            return "Aktif catalog lock kaydi bulunmadi.";
        }

        private static bool IsCatalogCouldNotBeResolved(CatalogLocksResult result)
        {
            return result != null &&
                   !result.Success &&
                   string.Equals(
                       result.Message,
                       "Catalog could not be resolved.",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildLockStatusText(IEnumerable<CatalogLockInfo> locks)
        {
            var lockTypes = (locks ?? Enumerable.Empty<CatalogLockInfo>())
                .Where(lockInfo => lockInfo != null)
                .Select(lockInfo => FormatLockTypeText(lockInfo.Lock))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return lockTypes.Count == 0
                ? "Lock"
                : string.Join(", ", lockTypes);
        }

        private static string FormatLockTypeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string text = value.Trim()
                .Replace("_", " ")
                .Replace("-", " ");

            text = Regex.Replace(text, "([a-z0-9])([A-Z])", "$1 $2");
            text = Regex.Replace(text, "\\s+", " ").Trim();

            if (string.Equals(text, text.ToUpperInvariant(), StringComparison.Ordinal))
                text = ToTitleCase(text);

            return text;
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var words = value
                .ToLowerInvariant()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1));

            return string.Join(" ", words);
        }

        private static string BuildApprovalStatusText(CatalogApprovalStatus approvalStatus)
        {
            if (approvalStatus == null)
                return "Onay bilgisi bulunamadi.";

            if (!string.IsNullOrWhiteSpace(approvalStatus.Status) &&
                !string.IsNullOrWhiteSpace(approvalStatus.StepText))
            {
                return approvalStatus.Status + " - " + approvalStatus.StepText;
            }

            bool hasStatus = !string.IsNullOrWhiteSpace(approvalStatus.Status);
            bool hasSteps = approvalStatus.Steps != null && approvalStatus.Steps.Count > 0;

            if (hasStatus && hasSteps)
                return approvalStatus.Status + " - " + approvalStatus.Steps.Count + " step";

            if (hasStatus)
                return approvalStatus.Status;

            return hasSteps
                ? approvalStatus.Steps.Count + " bekleyen onay step'i"
                : "Onay bilgisi bulunamadi.";
        }

        private static string BuildApprovalStepText(CatalogApprovalStep step)
        {
            if (step == null)
                return string.Empty;

            string stepName = string.IsNullOrWhiteSpace(step.StepName)
                ? "Step " + step.StepNumber
                : step.StepName;

            string approver = string.IsNullOrWhiteSpace(step.ApproverName)
                ? "Onayci bilinmiyor"
                : step.ApproverName;

            string group = string.IsNullOrWhiteSpace(step.GroupName)
                ? string.Empty
                : " / " + step.GroupName;

            return step.StepNumber + ". " + stepName + group + ": " + approver;
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

        private string ResolveModelOwnerText()
        {
            if (modelInfo == null)
                return "Model bilgisi yok.";

            string owner = FindPropertyValue(
                modelInfo.getoObjectProperty(),
                "version owner",
                "owner",
                "created by",
                "createdby",
                "creator",
                "created user",
                "modified by",
                "modifiedby");

            return string.IsNullOrWhiteSpace(owner)
                ? "Bulunamadi"
                : owner;
        }

        private string ResolveVersionName(string catalogName)
        {
            if (!string.IsNullOrWhiteSpace(catalogName) &&
                catalogName.Trim().StartsWith("Version", StringComparison.OrdinalIgnoreCase))
            {
                return catalogName.Trim();
            }

            string versionNumber = ResolveVersionNumber(catalogName);
            return string.IsNullOrWhiteSpace(versionNumber)
                ? Safe(catalogName, string.Empty)
                : "Version " + versionNumber;
        }

        private string ResolveVersionNumber(string catalogName)
        {
            string versionNumber = ExtractVersionNumber(catalogName);
            if (!string.IsNullOrWhiteSpace(versionNumber))
                return versionNumber;

            if (modelInfo != null)
            {
                versionNumber = ExtractVersionNumber(modelInfo.getoName());
                if (!string.IsNullOrWhiteSpace(versionNumber))
                    return versionNumber;

                versionNumber = FindPropertyValue(
                    modelInfo.getoObjectProperty(),
                    "version number",
                    "versionnumber",
                    "version no",
                    "version");

                versionNumber = ExtractVersionNumber(versionNumber);
                if (!string.IsNullOrWhiteSpace(versionNumber))
                    return versionNumber;
            }

            return string.Empty;
        }

        private static string ExtractVersionNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            Match match = Regex.Match(
                value,
                @"(?:^|\b)version\s*(?<number>\d+)(?:\b|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (match.Success)
                return match.Groups["number"].Value;

            match = Regex.Match(value, @"\b(?<number>\d+)\b", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["number"].Value : string.Empty;
        }

        private static string FindPropertyValue(
            IEnumerable<ObjectProperty> objectProperties,
            params string[] nameHints)
        {
            if (objectProperties == null || nameHints == null)
                return string.Empty;

            var properties = objectProperties
                .Where(property => property != null)
                .Select(property => new
                {
                    Name = NormalizePropertyName(property.getoPropertyClassName()),
                    Value = FirstNonEmpty(
                        property.getoPropertyFormatAsString(),
                        property.getoPropertyValue())
                })
                .Where(property => !string.IsNullOrWhiteSpace(property.Value))
                .ToList();

            foreach (string hint in nameHints)
            {
                string normalizedHint = NormalizePropertyName(hint);
                var match = properties.FirstOrDefault(
                    property => string.Equals(property.Name, normalizedHint, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    return match.Value;
            }

            foreach (string hint in nameHints)
            {
                string normalizedHint = NormalizePropertyName(hint);
                var match = properties.FirstOrDefault(
                    property => property.Name.IndexOf(normalizedHint, StringComparison.OrdinalIgnoreCase) >= 0);

                if (match != null)
                    return match.Value;
            }

            return string.Empty;
        }

        private static string NormalizePropertyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim()
                .ToLowerInvariant();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
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

        public sealed class ApprovalStepDisplayItem
        {
            public string StepText { get; set; }

            public string StatusText { get; set; }
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
