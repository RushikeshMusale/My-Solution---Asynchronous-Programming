using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;

namespace StockAnalyzer.Windows.Services
{
    public interface IStockService
    {
         Task<IEnumerable<StockPrice>> GetStockPricesFor(string ticker, CancellationToken token);
    }

    public class StockService: IStockService
    {
        private int i = 0;
        public async Task<IEnumerable<StockPrice>> GetStockPricesFor(string ticker, CancellationToken token)
        {
            await Task.Delay((i++) * 1000);
            using (var client = new HttpClient())
            {
                var result = await client.GetAsync($"http://localhost:61363/api/stocks/{ticker}", token);

                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<IEnumerable<StockPrice>>(content);
            }
        }
    }

    public class MockStockService : IStockService
    {
        public Task<IEnumerable<StockPrice>> GetStockPricesFor(string ticker, CancellationToken token)
        {
            List<StockPrice> stocks = new List<StockPrice>
            {
                new StockPrice{Ticker="MSFT", Change= .5m, ChangePercent=.75m},
                new StockPrice{Ticker="MSFT", Change= .3m, ChangePercent=.15m},
                new StockPrice{Ticker="GOOGL", Change= .3m, ChangePercent=.25m},
                new StockPrice{Ticker="GOOGL", Change= .5m, ChangePercent=.45m},
            };

            return Task.FromResult(stocks.Where(stock => stock.Ticker.Equals(ticker)));
        }
    }
}
