using System.Collections.ObjectModel;
using VeloxapEDGEWpfLib.Models;
using VeloxapEDGEWpfLib.Models.VeloxapEDGEWpfLib.Models;

namespace VeloxapEDGEWpfLib.ViewModels
{
    public class ModelInfoViewModel
    {
        public ObservableCollection<ModelItem> ModelItems { get; set; }

        public ObservableCollection<PropertyItem> Properties { get; set; }

        public ModelInfoViewModel()
        {
            ModelItems = new ObservableCollection<ModelItem>
            {
                new ModelItem { Name = "(Model) IETT" },

                new ModelItem { Name = "(Entity) D_ARAC_CC" },
                new ModelItem { Name = "(Entity) T_VV" },
                new ModelItem { Name = "(Entity) D_GUZERGAH" },
                new ModelItem { Name = "(Entity) D_HAT" },
                new ModelItem { Name = "(Entity) D_KILAVUZ" },
                new ModelItem { Name = "(Entity) D_TARIH" },
                new ModelItem { Name = "(Entity) F_SEFER" },
                new ModelItem { Name = "(Entity) T_DurC_ak2" },
                new ModelItem { Name = "(Entity) D_PERSONEL" },
                new ModelItem { Name = "(Entity) T_TESTTABLE" },

                new ModelItem { Name = "(Relationship) R/1" },
                new ModelItem { Name = "(Relationship) R/2" },
                new ModelItem { Name = "(Relationship) R/4" },
                new ModelItem { Name = "(Relationship) R/5" },
                new ModelItem { Name = "(Relationship) R/6" },
                new ModelItem { Name = "(Relationship) R/7" },
                new ModelItem { Name = "(Relationship) R_3" }
            };

            Properties = new ObservableCollection<PropertyItem>
            {
                new PropertyItem
                {
                    Property = "Auto_Created_Initial",
                    DataType = "I4",
                    Value = "1",
                    AsString = "1"
                },

                new PropertyItem
                {
                    Property = "Definition",
                    DataType = "Str",
                    Value = "model tanimi",
                    AsString = "model tanimi"
                },

                new PropertyItem
                {
                    Property = "Name",
                    DataType = "Str",
                    Value = "IETT",
                    AsString = "IETT"
                },

                new PropertyItem
                {
                    Property = "Long_Id",
                    DataType = "Id",
                    Value = "{799A0437-D91A-4CC5-998E}",
                    AsString = "{799A0437-D91A-4CC5-998E}"
                },

                new PropertyItem
                {
                    Property = "Owner_Path",
                    DataType = "Str",
                    Value = "",
                    AsString = ""
                },

                new PropertyItem
                {
                    Property = "Type",
                    DataType = "I4",
                    Value = "Physical",
                    AsString = "2"
                },

                new PropertyItem
                {
                    Property = "Naming_Standards",
                    DataType = "Str",
                    Value = "",
                    AsString = ""
                },

                new PropertyItem
                {
                    Property = "Allow_Special_Characters",
                    DataType = "Bool",
                    Value = "false",
                    AsString = "False"
                },

                new PropertyItem
                {
                    Property = "Remove_Vowels",
                    DataType = "Bool",
                    Value = "false",
                    AsString = "False"
                },

                new PropertyItem
                {
                    Property = "Special_Characters",
                    DataType = "I4",
                    Value = "0",
                    AsString = "0"
                },

                new PropertyItem
                {
                    Property = "Tracking_History_Object",
                    DataType = "I4",
                    Value = "1048575",
                    AsString = "1048575"
                },

                new PropertyItem
                {
                    Property = "Enforce_Unique_Name",
                    DataType = "I4",
                    Value = "NotUnique",
                    AsString = "0"
                },

                new PropertyItem
                {
                    Property = "Target_Server",
                    DataType = "I4",
                    Value = "SybaseIQ",
                    AsString = "197"
                },

                new PropertyItem
                {
                    Property = "DBMS_Major_Version",
                    DataType = "I4",
                    Value = "15",
                    AsString = "15"
                }
            };
        }
    }
}