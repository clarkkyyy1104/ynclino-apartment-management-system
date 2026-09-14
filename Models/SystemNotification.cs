namespace YnclinoApartmentManagementSystem.Models
{
    public class SystemNotification
    {
        public string Module { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Link { get; set; }

        public int? TargetId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}