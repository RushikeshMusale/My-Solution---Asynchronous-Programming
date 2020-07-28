using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;
using StockAnalyzer.Windows.Services;

namespace StockAnalyzer.Windows
{
    public partial class MainWindow : Window
    {
        CancellationTokenSource _cancellationTokenSource = null;
        public MainWindow()
        {
            InitializeComponent();
        }

        //private void Search_Click(object sender, RoutedEventArgs e)
        //{
        //    #region Before loading stock data
        //    var watch = new Stopwatch();
        //    watch.Start();
        //    StockProgress.Visibility = Visibility.Visible;
        //    StockProgress.IsIndeterminate = true;
        //    #endregion

        //    var client = new WebClient();

        //    var content = client.DownloadString($"http://localhost:61363/api/stocks/{Ticker.Text}");

        //    var data = JsonConvert.DeserializeObject<IEnumerable<StockPrice>>(content);

        //    Stocks.ItemsSource = data;

        //    #region After stock data is loaded
        //    StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
        //    StockProgress.Visibility = Visibility.Hidden;
        //    #endregion
        //}

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            #region Before loading stock data
            var watch = new Stopwatch();
            watch.Start();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = false;
            StockProgress.Maximum = Ticker.Text.Split(' ', ',').Count();
            StockProgress.Minimum = 0;
            Search.Content = "Cancel";
            #endregion

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                // Anther way to cancel task after some time;
                //_cancellationTokenSource.CancelAfter(2000);
                _cancellationTokenSource = null;
                return;
            }
            
            _cancellationTokenSource = new CancellationTokenSource();

            // we can also register a delegate to know when the cancellation is performed
            _cancellationTokenSource.Token.Register(() =>
            {
                Notes.Text += "Search is cancelled"; // this will run on calling thread (UI), no need for dispatcher
            });

            //Progress bar should progress when stocks for one of the tickers is loaded
            // we can do it by using Progress objects ProgressChanged event & Report method

            var progress = new Progress<IEnumerable<StockPrice>>();
            progress.ProgressChanged += (_, stocks) =>
            {
                Notes.Text += "Loaded Stocks" + stocks.Count() +" for "+ stocks.First().Ticker + Environment.NewLine;
                StockProgress.Value += 1;
            };
            
            try
            {
                await LoadStocks(progress);

            }catch (Exception ex)
            {
                Notes.Text += ex.Message + Environment.NewLine;
            }

        
            #region After stock data is loaded
            StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
            StockProgress.Visibility = Visibility.Hidden;
            Search.Content = "Search";
            #endregion
        }
       

        private async Task LoadStocks(IProgress<IEnumerable<StockPrice>> progress = null)
        {
            var tickers = Ticker.Text.Split(',', ' ');

            var service = new StockService();

            var tickerLoadingTasks = new List<Task<IEnumerable<StockPrice>>>();

            foreach (var ticker in tickers)
            {
                var loadTask = service.GetStockPricesFor(ticker, _cancellationTokenSource.Token);

                loadTask = loadTask.ContinueWith(stockTask =>
                {
                    progress?.Report(stockTask.Result);
                    return stockTask.Result;
                });
                tickerLoadingTasks.Add(loadTask);
            }

            var allStocks = await Task.WhenAll(tickerLoadingTasks);

            Stocks.ItemsSource = allStocks.SelectMany(stocks => stocks);
        }

        private Task<List<string>> SearchForStocks(CancellationToken cancellationToken)
        {          
            var loadLinesTask = Task.Run(async () =>
            {
                List<string> lines = new List<string>();
                using (var stream = new StreamReader(File.OpenRead(@"StockPrices_Small.csv")))
                {
                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        // Two ways to handle cancellation
                        // Option 1: to check if token cancellation is requested
                        if (cancellationToken.IsCancellationRequested)
                            return lines;
                        // Option 2: throw exception using token.ThrowIfCancellationRequested();
                        lines.Add(line);                       
                    }

                    return lines;
                }
            }, cancellationToken);

            return loadLinesTask;            
        }


        public async Task GetStocks()
        {
            using (var client = new HttpClient())
            {
                // Changed to url that does not exists, will throw the exception
                var response = await client.GetAsync($"http://localhost123:61363/myapi/stocks/{Ticker.Text}");
                // this throws exception if status code is not ok
                try
                {
                    response.EnsureSuccessStatusCode();

                    //client.GetAsync($"http://localhost:61363/api/stocks/{Ticker.Text}").Result;
                    //will make it synchronous. it can result it into deadlock in certain situations

                    var content = await response.Content.ReadAsStringAsync();

                    var data = JsonConvert.DeserializeObject<IEnumerable<StockPrice>>(content);

                    Stocks.ItemsSource = data;


                }
                catch (Exception ex)
                {
                    this.Notes.Text = ex.Message;
                }
            }

        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
