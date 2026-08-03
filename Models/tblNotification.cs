using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YnclinoApartmentManagementSystem.Models
{
    public class tblNotification
    {
        [Key]
        public int NotificationID { get; set; }

        // the user who should see this notification
        [Required]
        public int UserID { get; set; }

        // which module it belongs to (drives the per-module nav badge):
        // Billing | Maintenance | LostFound | Transfer
        [Required]
        [MaxLength(30)]
        public string Module { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Message { get; set; } = string.Empty;

        // where clicking the notification should take the user (optional)
        [MaxLength(300)]
        public string? Link { get; set; }

        // the specific record this notification is about (bill/request/item id),
        // so its row can be shown as unread until the user opens it
        public int? TargetId { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("UserID")]
        public tblUser? User { get; set; }
    }
}
