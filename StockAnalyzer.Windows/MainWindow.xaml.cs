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
using System.Windows.Navigation;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;

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

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            #region Before loading stock data
            var watch = new Stopwatch();
            watch.Start();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = true;
            Search.Content = "Cancel";
            #endregion


            // Since we changed the url, exception is generated. and if we use async void, 
            // exception will not be caught.
            // application will crash
            // await GetStocks(); -- Module 3 begins

            if(_cancellationTokenSource  != null  )
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
                return;               
            }

            _cancellationTokenSource = new CancellationTokenSource();

            // we can also register a delegate to know when the cancellation is performed
            _cancellationTokenSource.Token.Register(() =>
            {
                Notes.Text += "Search is cancelled"; // this will run on calling thread (UI), no need for dispatcher
            });

            // task.run will execute operation on differnt thread
            var loadLinesTask = SearchForStocks(_cancellationTokenSource.Token);

            // await keyword provides continuation block, but for the task, we have to manually do it
            // so that next operation is executed only when first operation is completed.            
            var processStocksTask = loadLinesTask.ContinueWith(t =>
            {
                var lines = t.Result;

                var data = new List<StockPrice>();
                foreach (var line in lines.Skip(1))
                {
                    var segments = line.Split(',');

                    for (var i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim('\'', '"');
                    var price = new StockPrice
                    {
                        Ticker = segments[0],
                        TradeDate = DateTime.ParseExact(segments[1], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                        Volume = Convert.ToInt32(segments[6], CultureInfo.InvariantCulture),
                        Change = Convert.ToDecimal(segments[7], CultureInfo.InvariantCulture),
                        ChangePercent = Convert.ToDecimal(segments[8], CultureInfo.InvariantCulture),
                    };
                    data.Add(price);
                }

                // Since task operation runs on different thread, we can't directly set objects on UI thread
                Dispatcher.Invoke(() =>
                {
                    Stocks.ItemsSource = data.Where(price => price.Ticker == Ticker.Text);
                });
            }, _cancellationTokenSource.Token,  // This token ensures that if cancellation is already requested, then task is not executed
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Current
            );

            loadLinesTask.ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    Notes.Text = t.Exception.InnerException.Message;
                });
            },TaskContinuationOptions.OnlyOnFaulted);

            processStocksTask.ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                        #region After stock data is loaded
                        StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
                        StockProgress.Visibility = Visibility.Hidden;
                        Search.Content = "Search";
                        #endregion
                });
            });

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
