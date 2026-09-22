namespace YnclinoApartmentManagementSystem.Models
{
    public class SystemNotification
    {
        public string Module { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Link { get; set; }

        public int? TargetId { get; set; }

        public DateTime CreatedAt { get; set; }

        // how much this adds to its module's badge in the sidebar: one by
        // default, the number of bills for "3 overdue bills", and nothing for a
        // reminder that belongs in the feed but is not yet outstanding
        public int BadgeCount { get; set; } = 1;
    }
}