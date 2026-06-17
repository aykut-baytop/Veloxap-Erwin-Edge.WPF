using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VeloxapEDGEWpfLib.Models;

namespace VeloxapEDGEWpfLib.Pages
{
    public partial class ModelUdpView : UserControl
    {
        private readonly List<UdpRow> allRows;
        private readonly ObservableCollection<UdpRow> visibleRows;

        public ModelUdpView()
            : this(null)
        {
        }

        internal ModelUdpView(ModelInfo modelInfo)
        {
            allRows = new List<UdpRow>();
            visibleRows = new ObservableCollection<UdpRow>();

            InitializeComponent();

            allRows.AddRange(BuildRows(modelInfo));
            gridUdp.ItemsSource = visibleRows;

            txtModelName.Text = BuildModelText(modelInfo);
            txtTableCount.Text = CountTables(modelInfo).ToString();
            txtUdpCount.Text = allRows.Count.ToString();

            ApplyFilter();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filter = txtSearch == null
                ? string.Empty
                : (txtSearch.Text ?? string.Empty).Trim();

            visibleRows.Clear();

            IEnumerable<UdpRow> rows = allRows;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                rows = rows.Where(row => Contains(row.TableName, filter) ||
                                         Contains(row.UdpName, filter) ||
                                         Contains(row.Value, filter) ||
                                         Contains(row.AsString, filter));
            }

            foreach (var row in rows)
                visibleRows.Add(row);

            txtVisibleCount.Text = visibleRows.Count.ToString();

            bool isEmpty = visibleRows.Count == 0;
            emptyState.Visibility = isEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;

            txtEmpty.Text = allRows.Count == 0
                ? "Secili model tablolarinda UDP bulunamadi."
                : "Arama kriterine uygun UDP bulunamadi.";

            txtStatus.Text = allRows.Count == 0
                ? "UDP bulunamadi."
                : visibleRows.Count + " UDP listeleniyor.";
        }

        private static List<UdpRow> BuildRows(ModelInfo modelInfo)
        {
            var rows = new List<UdpRow>();

            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            if (objects == null)
                return rows;

            foreach (var table in EnumerateTables(objects))
            {
                string tableName = Safe(table.getoName(), "(adsiz tablo)");
                var properties = table.getoObjectProperty();

                if (properties == null)
                    continue;

                foreach (var property in properties.Where(IsUdpProperty))
                {
                    rows.Add(new UdpRow
                    {
                        TableName = tableName,
                        UdpName = Safe(property.getoPropertyClassName(), "(adsiz UDP)"),
                        Value = Safe(property.getoPropertyValue(), string.Empty),
                        AsString = Safe(property.getoPropertyFormatAsString(), string.Empty),
                        DataType = Safe(property.getoPropertyType(), string.Empty)
                    });
                }
            }

            return rows
                .OrderBy(row => row.TableName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.UdpName, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

        private static int CountTables(ModelInfo modelInfo)
        {
            var objects = modelInfo == null
                ? null
                : modelInfo.getoModelObject();

            return EnumerateTables(objects).Count();
        }

        private static bool IsUdpProperty(ObjectProperty property)
        {
            if (property == null)
                return false;

            string propertyName = property.getoPropertyClassName();
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            return propertyName.Contains(".") ||
                   propertyName.IndexOf("UDP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   propertyName.IndexOf("User Defined", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildModelText(ModelInfo modelInfo)
        {
            if (modelInfo == null)
                return "Secili model yuklenmedi.";

            string name = Safe(modelInfo.getoName(), "Secili model");
            string location = Safe(modelInfo.getoLocation(), string.Empty);

            return string.IsNullOrWhiteSpace(location)
                ? name
                : name + " - " + location;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
        }

        public sealed class UdpRow
        {
            public string TableName { get; set; }

            public string UdpName { get; set; }

            public string Value { get; set; }

            public string AsString { get; set; }

            public string DataType { get; set; }
        }
    }
}
