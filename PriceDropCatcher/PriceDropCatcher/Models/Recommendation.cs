namespace PriceDropCatcher.Models
{
    public enum RecommendationVerdict
    {
        BuyNow,
        Wait,
        Neutral
    }

    public class Recommendation
    {
        public RecommendationVerdict Verdict { get; set; }
        public string Reason { get; set; }
    }
}
