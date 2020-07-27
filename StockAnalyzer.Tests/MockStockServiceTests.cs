using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockAnalyzer.Windows.Services;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockAnalyzer.Tests
{
    [TestClass]
    public class MockStockServiceTests
    {
        [TestMethod]
        public async Task Can_Load_All_MSFT_Stocks()
        {
            var mockStockService = new MockStockService();
            var stocks = await mockStockService.GetStockPricesFor("MSFT", CancellationToken.None);

            Assert.AreEqual(stocks.Count(), 2);
            Assert.AreEqual(stocks.Sum(stock => stock.Change), .8m);

        }

    }
}
