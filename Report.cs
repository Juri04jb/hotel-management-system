using HotelManagementApp.Models.Enums;

namespace HotelManagementApp.Models
{
    public class Report
    {
        public int ReportId { get; set; }
        public string ReportName { get; set; }
        public ReportType ReportType { get; set; }
        public DateTime CreationDate { get; set; }
        public string ReportData { get; set; }
        public int? CreatedBy { get; set; }

        public ApplicationUser CreatedByUser { get; set; }
    }
}
