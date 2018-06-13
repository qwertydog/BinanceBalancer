namespace BinanceBalancer.Models.Implementations
{
    internal class RateLimit
    {
        public RateLimitType RateLimitType { get; set; }
        public RateLimitInterval Interval { get; set; }
        public int Limit { get; set; }
    }
}
