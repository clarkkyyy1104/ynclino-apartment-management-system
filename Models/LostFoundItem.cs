namespace YnclinoApartmentManagementSystem.Models
{
    // Anyone with an account may report or claim an item, so these point at
    // accounts rather than at tenants.
    public class LostFoundItem
    {
        public int ItemID { get; set; }

        public int ReportedByUserID { get; set; }
        public int? ClaimedByUserID { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string ItemType { get; set; } = "Lost";      // Lost | Found
        public string? Location { get; set; }
        public string Status { get; set; } = "Reported";    // Reported | Claimed

        public DateTime DateReported { get; set; } = DateTime.Now;
        public DateTime? DateClaimed { get; set; }

        public string? Notes { get; set; }
        public string? ImagePath { get; set; }

        public User? ReportedBy { get; set; }
        public User? ClaimedBy { get; set; }
        public ICollection<ClaimRequest> Claims { get; set; } = new List<ClaimRequest>();
    }
}
