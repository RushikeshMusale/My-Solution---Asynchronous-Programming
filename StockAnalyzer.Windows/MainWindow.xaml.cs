using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using Newtonsoft.Json;
using StockAnalyzer.Core.Domain;

namespace StockAnalyzer.Windows
{
    public partial class MainWindow : Window
    {
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
            #endregion


            // Since we changed the url, exception is generated. and if we use async void, 
            // exception will not be caught.
            // application will crash
            // await GetStocks(); -- Module 3 begins


            // task.run will execute operation on differnt thread
            var loadLinesTask = Task.Run(() =>
            {
                var lines = File.ReadAllLines(@"StockPrices_Small.csv");
                return lines;
            });

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
            });

            processStocksTask.ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                        #region After stock data is loaded
                        StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
                        StockProgress.Visibility = Visibility.Hidden;
                        #endregion
                    });
            });

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
