using System;

namespace PriceDropCatcher.Models
{
    /// <summary>Current product session after URL processing.</summary>
    public class Product
    {
        public string SourceUrl { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string PagePriceDisplay { get; set; }
        public decimal? PagePriceUsd { get; set; }
        public string RetailerHint { get; set; }
        public DateTime LoadedAt { get; set; }
    }
}
