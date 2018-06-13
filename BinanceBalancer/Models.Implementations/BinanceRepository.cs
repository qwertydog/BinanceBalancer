using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace BinanceBalancer.Models.Implementations
{
    public class BinanceRepository : IBinanceRepository, IDisposable
    {
        #region Enums

        public enum OrderSide
        {
            BUY,
            SELL
        }

        public enum OrderType
        {
            LIMIT,
            MARKET,
            STOP_LOSS,
            STOP_LOSS_LIMIT,
            TAKE_PROFIT,
            TAKE_PROFIT_LIMIT,
            LIMIT_MAKER,
        }

        public enum TimeInForce
        {
            GTC, // (Good-Til-Canceled) orders are effective until they are executed or canceled.
            IOC, // (Immediate or Cancel) orders fills all or part of an order immediately and cancels the remaining part of the order.
            FOK  // (Fill or Kill) orders fills all in its entirety, otherwise, the entire order will be cancelled.
        }

        #endregion

        #region Const fields

        private const string ApiKeyHeaderName = "X-MBX-APIKEY";
        private const string ApiEndpoint = @"https://api.binance.com";

        private const string PingEndpoint = @"api/v1/ping";
        private const string ServerTimeEndpoint = @"api/v1/time";
        private const string ExchangeInfoEndpoint = @"api/v1/exchangeInfo";
        private const string OrderBookEndpoint = @"api/v1/depth";
        private const string RecentTradesEndpoint = @"api/v1/trades";
        private const string Price24HrTickerEndpoint = @"api/v1/ticker/24hr";
        private const string CurrentPriceEndpoint = @"api/v3/ticker/price";
        private const string BestOrderPriceEndpoint = @"api/v3/ticker/bookTicker";
        private const string NewOrderEndpoint = @"api/v3/order";
        private const string TestOrderEndpoint = @"api/v3/order/test";
        private const string OpenOrdersEndpoint = @"api/v3/openOrders";
        private const string AccountInfoEndpoint = @"api/v3/account";
        private const string AccountTradesEndpoint = @"api/v3/myTrades";

        private const decimal MinimumLimitTradeOrderInBitcoin = 0.001M;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        #endregion

        #region Static fields

        private static readonly object _serverTimeOffsetLock = new object();
        private static long _serverOffsetInMilliseconds;
        private static bool _serverTimeOffsetSet;

        private static readonly object _rateLimitLock = new object();
        private static BinanceRateLimiter _rateLimiter;
        private static bool _rateLimitsSet;

        private static readonly object _currenciesLock = new object();
        private static HashSet<Currency> _currencies;
        private static HashSet<CurrencyPair> _currencyPairs;
        private static bool _currenciesSet;

        #endregion

        #region Member fields

        private readonly string _apiKey, _apiSecret;
        private readonly HttpClient _httpClient;

        #endregion

        #region Constructors

        private BinanceRepository(string apiKey, string apiSecret)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;

            var httpClientHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };

            _httpClient = new HttpClient(httpClientHandler, true)
            {
                BaseAddress = new Uri(ApiEndpoint)
            };

            var mediaType = new MediaTypeWithQualityHeaderValue("application/json");

            _httpClient.DefaultRequestHeaders.Accept.Add(mediaType);
            _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, _apiKey);
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        #endregion

        #region Public static methods

        public static async Task<BinanceRepository> Create(string apiKey, string apiSecret)
        {
            var binanceRepository = new BinanceRepository(apiKey, apiSecret);

            await binanceRepository.InitialiseExchangeInfo().ConfigureAwait(false);

            return binanceRepository;
        }

        #endregion

        #region Public methods

        public async Task<bool> IsServerOnline()
        {
            var response = await _httpClient.GetAsync(PingEndpoint);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        public async Task<long> GetServerTimeStampInMilliseconds()
        {
            var response = await _httpClient.GetAsync(ServerTimeEndpoint);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();

            var serverTime = result.Split(new[] { ':' })[1].Replace("}", "");

            return Convert.ToInt64(serverTime);
        }

        public async Task GetOrderBook(CurrencyPair currencyPair, int limit)
        {
            Contract.Requires(limit == 5 || limit == 10 || limit == 20 || limit == 50 ||
                              limit == 100 || limit == 500 || limit == 1000);

            await GetOrderBookResults(new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) },
                { "limit", Convert.ToString(limit) }
            })
            .ConfigureAwait(false);
        }

        public async Task GetOrderBook(CurrencyPair currencyPair)
        {
            await GetOrderBookResults(new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            })
            .ConfigureAwait(false);
        }

        public async Task GetRecentTrades(CurrencyPair currencyPair, int limit)
        {
            Contract.Requires(limit > 0 && limit <= 500);

            await GetRecentTradesResults(new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) },
                { "limit", Convert.ToString(limit) }
            })
            .ConfigureAwait(false);
        }

        public async Task GetRecentTrades(CurrencyPair currencyPair)
        {
            await GetRecentTradesResults(new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            })
            .ConfigureAwait(false);
        }

        public async Task Get24hrPriceChange(CurrencyPair currencyPair)
        {
            var queryParameter = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = Price24HrTickerEndpoint,
                Query = GetQueryString(queryParameter)
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri).ConfigureAwait(false);

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            MessageBox.Show(content);
        }

        public async Task GetAllPrices()
        {
            var response = await _httpClient.GetAsync(CurrentPriceEndpoint).ConfigureAwait(false);

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            MessageBox.Show(content);
        }

        public async Task GetPrice(CurrencyPair currencyPair)
        {
            var queryParameter = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = CurrentPriceEndpoint,
                Query = GetQueryString(queryParameter)
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task GetBestOrderPrices()
        {
            var response = await _httpClient.GetAsync(BestOrderPriceEndpoint);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task GetBestOrderPrice(CurrencyPair currencyPair)
        {
            var queryParameter = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = BestOrderPriceEndpoint,
                Query = GetQueryString(queryParameter)
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task PostNewMarketOrder(CurrencyPair currencyPair, OrderSide side, OrderType type, decimal quantity)
        {
            Contract.Requires(quantity >= 0);

            var queryParameters = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) },
                { "side", Enum.GetName(typeof(OrderSide), side) },
                { "type", Enum.GetName(typeof(OrderType), type) },
                { "quantity", quantity.ToString() }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = NewOrderEndpoint,
                Query = GetQueryStringWithSignedParameters(queryParameters)
            };

            var response = await _httpClient.PostAsync(uriBuilder.Uri, null);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task PostNewLimitOrder(CurrencyPair currencyPair, OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            Contract.Requires(quantity >= 0);
            Contract.Requires(price >= 0);

            var queryParameters = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) },
                { "side", Enum.GetName(typeof(OrderSide), side) },
                { "type", Enum.GetName(typeof(OrderType), type) },
                { "quantity", quantity.ToString() },
                { "price", price.ToString() },
                { "quantity", Enum.GetName(typeof(TimeInForce), timeInForce) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = NewOrderEndpoint,
                Query = GetQueryStringWithSignedParameters(queryParameters)
            };

            var response = await _httpClient.PostAsync(uriBuilder.Uri, null);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task<bool> PostTestMarketOrder(CurrencyPair currencyPair, OrderSide side, OrderType type, decimal quantity)
        {
            Contract.Requires(quantity >= 0);

            var queryParameters = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) },
                { "side", Enum.GetName(typeof(OrderSide), side) },
                { "type", Enum.GetName(typeof(OrderType), type) },
                { "quantity", quantity.ToString() }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = TestOrderEndpoint,
                Query = GetQueryStringWithSignedParameters(queryParameters)
            };

            var response = await _httpClient.PostAsync(uriBuilder.Uri, null);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        public async Task<bool> PostTestLimitOrder(CurrencyPair currencyPair, OrderSide side, OrderType type, decimal quantity, decimal price, TimeInForce timeInForce)
        {
            Contract.Requires(quantity >= 0);
            Contract.Requires(price >= 0);

            var queryParameters = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) },
                { "side", Enum.GetName(typeof(OrderSide), side) },
                { "type", Enum.GetName(typeof(OrderType), type) },
                { "quantity", quantity.ToString() },
                { "price", price.ToString() },
                { "quantity", Enum.GetName(typeof(TimeInForce), timeInForce) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = TestOrderEndpoint,
                Query = GetQueryStringWithSignedParameters(queryParameters)
            };

            var response = await _httpClient.PostAsync(uriBuilder.Uri, null);

            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        public async Task GetOpenOrders(CurrencyPair currencyPair)
        {
            var queryParameter = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = OpenOrdersEndpoint,
                Query = GetQueryStringWithSignedParameters(queryParameter)
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task GetAccountInformation()
        {
            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = AccountInfoEndpoint,
                Query = GetQueryStringWithSignedParameters()
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri);

            MessageBox.Show(await response.Content.ReadAsStringAsync());
        }

        public async Task GetAccountTrades(CurrencyPair currencyPair)
        {
            var queryParameter = new Dictionary<string, string>
            {
                { "symbol", Enum.GetName(typeof(CurrencyPair), currencyPair) }
            };

            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = AccountTradesEndpoint,
                Query = GetQueryStringWithSignedParameters(queryParameter)
            };

            var content = await GetRequest(uriBuilder.Uri);

            MessageBox.Show(content);
        }

        #endregion

        #region Static helper methods

        private static string GetQueryString(IDictionary<string, string> keyValuePairs)
        {
            var queryStringBuilder = new StringBuilder();

            var lastKey = keyValuePairs.Keys.Last();

            foreach (var keyValuePair in keyValuePairs.Where(x => x.Value.Any()))
            {
                queryStringBuilder.Append(keyValuePair.Key + "=" + keyValuePair.Value);

                if (keyValuePair.Key != lastKey)
                    queryStringBuilder.Append("&");
            }

            return queryStringBuilder.ToString();
        }

        private static long GetMillisecondsSince(DateTime epoch)
        {
            var milliseconds = (DateTime.UtcNow - epoch).TotalMilliseconds;

            return Convert.ToInt64(milliseconds);
        }

        private static long GetTimeStampInMilliseconds() 
            => GetMillisecondsSince(UnixEpoch) + _serverOffsetInMilliseconds;

        private static void SetServerTimeOffset(long serverTime)
        {
            if (_serverTimeOffsetSet) return;

            lock (_serverTimeOffsetLock)
            {
                if (!_serverTimeOffsetSet)
                {
                    _serverOffsetInMilliseconds = serverTime - GetMillisecondsSince(UnixEpoch);
                    _serverTimeOffsetSet = true;
                }
            }
        }

        private static void SetCurrencies(JEnumerable<JToken> jsonCurrencies)
        {
            if (_currenciesSet) return;

            lock (_currenciesLock)
            {
                _currencies = new HashSet<Currency>();
                _currencyPairs = new HashSet<CurrencyPair>();

                foreach (var symbol in jsonCurrencies)
                {
                    if (symbol["status"].ToObject<string>() != "TRADING") continue;

                    Enum.TryParse(symbol["baseAsset"].ToObject<string>(), out Currency baseCurrency);
                    _currencies.Add(baseCurrency);

                    Enum.TryParse(symbol["quoteAsset"].ToObject<string>(),
                        out Currency quoteCurrency);

                    _currencies.Add(quoteCurrency);

                    var currencyPair = new CurrencyPair(baseCurrency, quoteCurrency);

                    _currencyPairs.Add(currencyPair);
                }

                _currenciesSet = true;
            }
        }

        private static void SetRateLimits(JEnumerable<JToken> jsonRateLimits)
        {
            if (_rateLimitsSet) return;

            lock (_rateLimitLock)
            {
                var rateLimits = new List<RateLimit>();
                foreach (var jsonRateLimit in jsonRateLimits)
                {
                    rateLimits.Add(jsonRateLimit.ToObject<RateLimit>());
                }

                _rateLimiter = new BinanceRateLimiter(rateLimits);

                _rateLimitsSet = true;
            }
        }

        private static void StartRateLimiter()
        {
            //throw new NotImplementedException();
        }

        #endregion

        #region Helper methods

        private string GetQueryStringWithSignedParameters()
            => GetQueryStringWithSignedParameters(new Dictionary<string, string>());

        private string GetQueryStringWithSignedParameters(IDictionary<string, string> queryParameters)
        {
            queryParameters.Add("timestamp", Convert.ToString(GetTimeStampInMilliseconds()));

            return GetQueryStringWithSignature(queryParameters);
        }

        private string GetQueryStringWithSignature(IDictionary<string, string> queryParameters)
        {
            var queryString = GetQueryString(queryParameters);

            queryParameters.Add("signature", CalculateHmacSha256Hash(queryString));

            return GetQueryString(queryParameters);
        }

        private string CalculateHmacSha256Hash(string queryString)
        {
            var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret));
            var computedHash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(queryString));

            return BitConverter.ToString(computedHash).Replace("-", "");
        }

        private async Task InitialiseExchangeInfo()
        {
            if (_serverTimeOffsetSet && _rateLimitsSet && _currenciesSet) return;

            var response = await _httpClient.GetAsync(ExchangeInfoEndpoint).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var jsonContent = JObject.Parse(content);

            SetServerTimeOffset((long)jsonContent["serverTime"]);
            SetRateLimits(jsonContent["rateLimits"].Children());
            SetCurrencies(jsonContent["symbols"].Children());

            //StartRateLimiter();
        }

        private Task GetOrderBookResults(Dictionary<string, string> queryParameters)
        {
            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = OrderBookEndpoint,
                Query = GetQueryString(queryParameters)
            };

            return GetRequest(uriBuilder.Uri);
        }

        private async Task GetRecentTradesResults(Dictionary<string, string> queryParameters)
        {
            var uriBuilder = new UriBuilder(_httpClient.BaseAddress)
            {
                Path = RecentTradesEndpoint,
                Query = GetQueryString(queryParameters)
            };

            var response = await _httpClient.GetAsync(uriBuilder.Uri).ConfigureAwait(false);

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> GetRequest(Uri uri)
        {
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> PostRequest(Uri uri)
        {
            var response = await _httpClient.PostAsync(uri, null).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        #endregion
    }
}
