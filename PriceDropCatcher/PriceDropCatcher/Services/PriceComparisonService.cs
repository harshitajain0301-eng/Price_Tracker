using System.Collections.Generic;
using System.Linq;
using PriceDropCatcher.Models;

namespace PriceDropCatcher.Services
{
    public static class PriceComparisonService
    {
        public static PriceComparisonResult Build(IReadOnlyList<PriceResult> rows)
        {
            var withPrice = rows?.Where(r => r.PriceUsd.HasValue && r.PriceUsd.Value > 0).ToList()
                            ?? new List<PriceResult>();

            if (withPrice.Count == 0)
            {
                return new PriceComparisonResult
                {
                    Rows = rows ?? new List<PriceResult>(),
                    LowestPrice = null,
                    AveragePrice = null,
                    BestDealRow = null
                };
            }

            var min = withPrice.Min(r => r.PriceUsd.Value);
            var avg = withPrice.Average(r => (double)r.PriceUsd.Value);

            foreach (var r in rows ?? new List<PriceResult>())
                r.IsBestDeal = r.PriceUsd.HasValue && r.PriceUsd.Value == min;

            var best = withPrice.FirstOrDefault(r => r.PriceUsd == min);

            return new PriceComparisonResult
            {
                Rows = rows ?? new List<PriceResult>(),
                LowestPrice = min,
                AveragePrice = (decimal)avg,
                BestDealRow = best
            };
        }
    }
}
