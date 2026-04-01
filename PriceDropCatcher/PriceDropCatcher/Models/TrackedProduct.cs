using System;

namespace PriceDropCatcher.Models
{
    public class TrackedProduct
    {
        public DateTime AddedAt { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string Host { get; set; }
        public string Price { get; set; }
        public string Currency { get; set; }
    }
}

