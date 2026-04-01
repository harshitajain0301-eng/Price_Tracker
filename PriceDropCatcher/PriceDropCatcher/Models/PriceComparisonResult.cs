using System.Collections.Generic;

namespace PriceDropCatcher.Models
{
    public class PriceComparisonResult
    {
        public decimal? LowestPrice { get; set; }
        public decimal? AveragePrice { get; set; }
        public PriceResult BestDealRow { get; set; }
        public IReadOnlyList<PriceResult> Rows { get; set; }
    }
}
