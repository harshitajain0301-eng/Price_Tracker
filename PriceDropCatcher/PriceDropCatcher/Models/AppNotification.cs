using System;

namespace PriceDropCatcher.Models
{
    public enum NotificationKind
    {
        Info,
        PriceDrop,
        BestDeal,
        Error
    }

    public class AppNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public NotificationKind Kind { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
