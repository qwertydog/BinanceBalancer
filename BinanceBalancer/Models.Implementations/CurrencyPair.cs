using System;

namespace BinanceBalancer.Models.Implementations
{
    public class CurrencyPair
    {
        public string Symbol { get; }
        public Currency BaseCurrency { get; }
        public Currency QuoteCurrency { get; }

        public CurrencyPair(Currency baseCurrency, Currency quoteCurrency)
        {
            Symbol = Enum.GetName(typeof(Currency), baseCurrency) + 
                Enum.GetName(typeof(Currency), quoteCurrency);

            BaseCurrency = baseCurrency;
            QuoteCurrency = quoteCurrency;
        }
    }
}
