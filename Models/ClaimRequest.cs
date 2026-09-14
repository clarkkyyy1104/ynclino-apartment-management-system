namespace YnclinoApartmentManagementSystem.Models
{
    public class ClaimRequest
    {
        public int ClaimID { get; set; }

        public int ItemID { get; set; }
        public int ClaimantUserID { get; set; }

        public string VerificationDetails { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected

        public string? AdminNotes { get; set; }
        public string? ImagePath { get; set; }

        public LostFoundItem? Item { get; set; }
        public User? Claimant { get; set; }
    }
}
