using System;
using System.Text;

namespace Veloxap.AddIn.Erwin.Models
{
    public sealed class MainModelSelectionInfo
    {
        public static readonly MainModelSelectionInfo Empty = new MainModelSelectionInfo(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            -1,
            DateTime.MinValue);

        public MainModelSelectionInfo(
            string rawName,
            string displayName,
            string objectId,
            string persistenceObjectId,
            string ruleModelName,
            string ruleModelLongId,
            string versionNo,
            int selectedIndex,
            DateTime changedAt)
        {
            RawName = rawName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ObjectId = objectId ?? string.Empty;
            PersistenceObjectId = persistenceObjectId ?? string.Empty;
            RuleModelName = ruleModelName ?? string.Empty;
            RuleModelLongId = ruleModelLongId ?? string.Empty;
            VersionNo = versionNo ?? string.Empty;
            SelectedIndex = selectedIndex;
            ChangedAt = changedAt;
        }

        public string RawName { get; private set; }

        public string DisplayName { get; private set; }

        public string ObjectId { get; private set; }

        public string PersistenceObjectId { get; private set; }

        public string RuleModelName { get; private set; }

        public string RuleModelLongId { get; private set; }

        public string VersionNo { get; private set; }

        public int SelectedIndex { get; private set; }

        public DateTime ChangedAt { get; private set; }

        public bool HasSelection
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RawName) ||
                       !string.IsNullOrWhiteSpace(ObjectId) ||
                       !string.IsNullOrWhiteSpace(PersistenceObjectId);
            }
        }

        public string ToDisplayText()
        {
            if (!HasSelection)
                return "Secili ana model bulunamadi.";

            var builder = new StringBuilder();
            builder.AppendLine("Secili ana model bilgileri");
            builder.AppendLine();
            builder.AppendLine("Degisiklik zamani: " + FormatChangedAt());
            builder.AppendLine("Gorunen ad: " + DisplayName);
            builder.AppendLine("Ham deger: " + RawName);
            builder.AppendLine("ObjectId: " + ObjectId);
            builder.AppendLine("PersistenceObjectId: " + PersistenceObjectId);
            builder.AppendLine("SelectedIndex: " + SelectedIndex);
            builder.AppendLine("Rule model adi: " + RuleModelName);
            builder.AppendLine("Rule model long id: " + RuleModelLongId);
            builder.AppendLine("Versiyon: " + VersionNo);

            return builder.ToString();
        }

        private string FormatChangedAt()
        {
            return ChangedAt == DateTime.MinValue
                ? string.Empty
                : ChangedAt.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    public sealed class MainModelSelectionChangedEventArgs : EventArgs
    {
        public MainModelSelectionChangedEventArgs(MainModelSelectionInfo selectionInfo)
        {
            SelectionInfo = selectionInfo ?? MainModelSelectionInfo.Empty;
        }

        public MainModelSelectionInfo SelectionInfo { get; private set; }
    }
}
