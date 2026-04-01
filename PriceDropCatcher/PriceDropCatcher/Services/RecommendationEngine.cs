using System.Collections.Generic;
using System.Linq;
using PriceDropCatcher.Models;

namespace PriceDropCatcher.Services
{
    public static class RecommendationEngine
    {
        /// <summary>
        /// Uses page price when present; otherwise compares market low vs average.
        /// BUY: close to lowest; WAIT: clearly above market; else NEUTRAL.
        /// </summary>
        public static Recommendation Evaluate(
            PriceComparisonResult comparison,
            decimal? pagePriceUsd)
        {
            var rows = comparison?.Rows;
            var prices = rows?.Select(r => r.PriceUsd).Where(p => p.HasValue && p.Value > 0).Select(p => p.Value).ToList()
                         ?? new List<decimal>();
            if (prices.Count == 0)
                return new Recommendation
                {
                    Verdict = RecommendationVerdict.Wait,
                    Reason = "No comparable Amazon / Walmart / eBay prices yet. Try again or refine the product name."
                };

            var lowest = prices.Min();
            var avg = prices.Average();

            if (pagePriceUsd.HasValue && pagePriceUsd.Value > 0)
            {
                var p = pagePriceUsd.Value;
                if (p <= lowest * 1.03m)
                    return new Recommendation
                    {
                        Verdict = RecommendationVerdict.BuyNow,
                        Reason = "Your listed price is at or very near the best current offer in this sample."
                    };
                if (p > (decimal)avg * 1.05m)
                    return new Recommendation
                    {
                        Verdict = RecommendationVerdict.Wait,
                        Reason = "Your listed price sits above the average of current marketplace offers."
                    };
                return new Recommendation
                {
                    Verdict = RecommendationVerdict.Neutral,
                    Reason = "Your price is between the best deal and the market average."
                };
            }

            if (lowest <= (decimal)avg * 0.92m)
                return new Recommendation
                {
                    Verdict = RecommendationVerdict.BuyNow,
                    Reason = "The best current offer is noticeably below the average across platforms."
                };
            if (lowest >= (decimal)avg * 1.05m)
                return new Recommendation
                {
                    Verdict = RecommendationVerdict.Wait,
                    Reason = "Offers are clustered high relative to the sample average."
                };
            return new Recommendation
            {
                Verdict = RecommendationVerdict.Neutral,
                Reason = "Prices are in a typical band — no strong buy or wait signal from this snapshot."
            };
        }
    }
}
