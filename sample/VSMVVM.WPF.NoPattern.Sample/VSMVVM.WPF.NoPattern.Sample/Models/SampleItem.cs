using System.Collections.Generic;

namespace VSMVVM.WPF.NoPattern.Sample.Models
{
    public class SampleItem
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
        public string Status { get; set; } = "";

        public static List<SampleItem> CreateDemoList() => new()
        {
            new() { Name = "Item A", Value = 100, Status = "Active" },
            new() { Name = "Item B", Value = 250, Status = "Pending" },
            new() { Name = "Item C", Value = 75,  Status = "Inactive" },
            new() { Name = "Item D", Value = 500, Status = "Active" },
            new() { Name = "Item E", Value = 180, Status = "Pending" },
        };
    }
}
