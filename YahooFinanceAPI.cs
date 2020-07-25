using System;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace CupAndHandle
{
    /// <summary>
    /// YahooFinanceのWeb API呼び出しメソッド
    /// </summary>
    static class YahooFinanceAPI
    {
        /// <summary>
        /// 株価取得のメソッド
        /// </summary>
        /// <param name="symbol">NYSEのTicker Symbol</param>
        /// <param name="FromDate">開始日時</param>
        /// <param name="ToDate">終了日時</param>
        /// <returns></returns>
        public static List<HistoryPrice> GetStockPrices(string symbol, DateTime FromDate, DateTime ToDate)
        { 
            //Check local cache
            string filepath = @"StockData\" + symbol + "_"
                + FromDate.ToShortDateString().Replace(@"/", "") + "_" +
                ToDate.ToShortDateString().Replace(@"/", "") + ".txt";
            
            if (File.Exists(filepath))
            {
                return GetStockPricesFromFile(symbol, FromDate, ToDate);
            }
            else
            {
                Task<List<HistoryPrice>> historyData = Task.Run(() =>
                {
                    return YahooFinanceAPI.GetPriceAsync(symbol, FromDate, ToDate);
                });

                //株価データをローカルに保存
                SaveStockPricesInFile(symbol, FromDate, ToDate, historyData.Result);

                return historyData.Result;
            }
        }
        /// <summary>
        /// 株価データをローカルキャッシュからロードする
        /// </summary>
        /// <param name="symbol">Ticker Symbol</param>
        /// <param name="FromDate">開始日</param>
        /// <param name="ToDate">終了日</param>
        /// <returns></returns>
        private static List<HistoryPrice> GetStockPricesFromFile (string symbol, DateTime FromDate, DateTime ToDate)
        {
            //キャッシュのパス
            string[] pricelines = File.ReadAllLines(@"StockData\" + symbol + "_"
                + FromDate.ToShortDateString().Replace(@"/", "") + "_" +
                ToDate.ToShortDateString().Replace(@"/", "") + ".txt");

            List<HistoryPrice> prices = new List<HistoryPrice>();
            foreach (var line in pricelines)
            {
                var h = new HistoryPrice();
                string[] items = line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                h.Date = DateTime.ParseExact(items[0], "yyyy/MM/dd", null);
                h.Open = double.Parse(items[1]);
                h.Close = double.Parse(items[2]);
                h.High = double.Parse(items[3]);
                h.Low = double.Parse(items[4]);
                h.Volume = long.Parse(items[5]);

                prices.Add(h);
            }

            return prices;
        
        }

        /// <summary>
        /// 株価をローカルにキャッシュする
        /// </summary>
        /// <param name="symbol">Ticker Symbo</param>
        /// <param name="FromDate">開始日</param>
        /// <param name="ToDate">終了日</param>
        /// <param name="prices"></param>
        private static void SaveStockPricesInFile(string symbol, DateTime FromDate, DateTime ToDate, List<HistoryPrice> prices)
        {
            var sb = new StringBuilder();
            foreach (var price in prices)
            {
                sb.Append(price.Date.ToShortDateString().ToString() + "\t");
                sb.Append(price.Open.ToString() + "\t");
                sb.Append(price.Close.ToString() + "\t");
                sb.Append(price.High.ToString() + "\t");
                sb.Append(price.Low.ToString() + "\t");
                sb.AppendLine(price.Volume.ToString());
            }

            if (!Directory.Exists(@"StockData"))
            {
                Directory.CreateDirectory(@"StockData");
            } 

            ///データをファイルに書き出す
            File.WriteAllText(@"StockData\" + symbol + "_" 
                + FromDate.ToShortDateString().Replace(@"/", "") + "_" +
                ToDate.ToShortDateString().Replace(@"/", "") + ".txt", sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// CSVデータをHistoryPriveに変換
        /// 参考：https://github.com/dennislwy/YahooFinanceAPI/tree/develop/YahooFinanceAPI
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static async Task<List<HistoryPrice>> GetPriceAsync(string symbol, DateTime start, DateTime end)
        {
            try
            {
                var csvData = await GetRawAsync(symbol, start, end).ConfigureAwait(false);
                if (csvData != null)
                    return await ParsePriceAsync(csvData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return new List<HistoryPrice>();
        }

        /// <summary>
        /// Web APIの呼び出し
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="eventType"></param>
        /// <returns></returns>
        public static async Task<string> GetRawAsync(string symbol, DateTime start, DateTime end, string eventType = "history")
        {
            string csvData = null;

            try
            {
                var url = "https://query1.finance.yahoo.com/v7/finance/download/{0}?period1={1}&period2={2}&interval=1d&events={3}&crumb={4}";

                url = string.Format(url, symbol, Math.Round(ToUnixTimestamp(start), 0),
                    Math.Round(ToUnixTimestamp(end), 0), eventType, "");

                using (var wc = new WebClient())
                {
                    csvData = await wc.DownloadStringTaskAsync(url).ConfigureAwait(false);
                }
            }
            catch (WebException webEx)
            {
                var response = (HttpWebResponse)webEx.Response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return csvData;
        }
        private static double ToUnixTimestamp(DateTime datetime)
        {
            return (datetime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static async Task<List<HistoryPrice>> ParsePriceAsync(string csvData)
        {
            return await Task.Run(() =>
            {
                var lst = new List<HistoryPrice>();

                try
                {
                    var rows = csvData.Split(Convert.ToChar(10));

                    for (var i = 1; i <= rows.Length - 1; i++)
                    {
                        var row = rows[i];
                        if (string.IsNullOrEmpty(row)) continue;

                        var cols = row.Split(',');
                        if (cols[1] == "null") continue;

                        var itm = new HistoryPrice
                        {
                            Date = DateTime.Parse(cols[0]),
                            Open = Convert.ToDouble(cols[1]),
                            High = Convert.ToDouble(cols[2]),
                            Low = Convert.ToDouble(cols[3]),
                            Close = Convert.ToDouble(cols[4]),
                            AdjClose = Convert.ToDouble(cols[5])
                        };

                        if (cols[6] != "null") itm.Volume = Convert.ToInt64(cols[6]);

                        lst.Add(itm);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                return lst;
            }).ConfigureAwait(false);
        }


    }

    /// <summary>
    /// HistoryPriveクラス
    /// </summary>
    class HistoryPrice
    {
        public DateTime Date { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public double AdjClose { get; set; }
    }
}
