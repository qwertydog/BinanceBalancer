using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceBalancer.Models.Implementations
{
    internal class BinanceRateLimiter : IDisposable
    {
        private readonly List<RateLimit> _rateLimits;

        private object _weightLock = new object();
        private int _currentOrderWeight, _currentRequestWeight;

        private Timer _timer;

        public BinanceRateLimiter(List<RateLimit> rateLimits)
        {
            _rateLimits = rateLimits;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            _timer?.Dispose();
            _timer = new Timer(ClearWeights, null, 0, GetTimerPeriod());
        }

        public void Stop()
        {
            _timer?.Dispose();
        }

        private int GetTimerPeriod()
        {
            var timerPeriodInMilliseconds = 1000 * 60 * 60 * 24; // Default to 1 day (86,400,000)

            foreach (var rateLimit in _rateLimits)
            {
                switch (rateLimit.Interval)
                {
                    case RateLimitInterval.MINUTE:
                        var minuteInMilliseconds = 1000 * 60;
                        if (minuteInMilliseconds < timerPeriodInMilliseconds)
                            timerPeriodInMilliseconds = minuteInMilliseconds;
                        break;
                    case RateLimitInterval.SECOND:
                        var secondInMilliseconds = 1000;
                        if (secondInMilliseconds < timerPeriodInMilliseconds)
                            timerPeriodInMilliseconds = secondInMilliseconds;

                        return timerPeriodInMilliseconds;
                }
            }

            return timerPeriodInMilliseconds;
        }

        private void ClearWeights(object stateInfo)
        {
            foreach (var rateLimit in _rateLimits)
            {
                switch (rateLimit.Interval)
                {
                    case RateLimitInterval.DAY:

                        break;
                    case RateLimitInterval.MINUTE:

                        break;
                    case RateLimitInterval.SECOND:

                        break;
                }
            }
        }

        public void Execute()
        {

        }

        private void CanExecute(int weight, bool isOrder)
        {
            foreach (var rateLimit in _rateLimits)
            {
                //switch (rateLimit.)
            }

            /*_rwLock.EnterReadLock();

            lock (_weightLock)
            {
                foreach (var rateLimit in _rateLimits)
                {
                    if (rateLimit.RateLimitType == RateLimitType.REQUESTS)
                    {
                        long _maxWeight;

                        switch (rateLimit.Limit)
                        {

                        }
                    }
                }
            }

            _rwLock.ExitReadLock();*/
        }
    }
}
