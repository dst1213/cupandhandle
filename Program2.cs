using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CupAndHandle
{
    class Program
    {
        static void Main(string[] args)
        {
            //調べたい銘柄をリストする
            string[] stocks = new string[] { "MSFT", "AAPL", "GOOG", "AMZN" };

            //最高の勝率とその際の保有期間
            double BestRate = 0;
            double BestHold = 0;

            //保有期間を20日から120日まで５日ステップでチェックする
            for (int h = 20; h <= 120; h+=5)
            {
                Console.WriteLine("保有期間: " + h.ToString() + "日 ============");
                double totalRate = 0;

                //指定した銘柄をそれぞれカップ分析する
                foreach (var stock in stocks)
                {
                    //過去の株価を取得
                    List<HistoryPrice> history =
                        YahooFinanceAPI.GetStockPrices(stock, new DateTime(2008, 7, 1), new DateTime(2020, 7, 1));
                    //カップ分析＋勝率の計算
                    double rate = GetWinningRate(history, 60, 120, h);
                    Console.WriteLine(stock + ": " + rate.ToString());
                    totalRate += rate;
                }

                Console.WriteLine("勝率: " + (totalRate / stocks.Length).ToString() + "\n") ;

                //最高勝率をチェックする
                if (BestRate < totalRate)
                {
                    BestRate = totalRate;
                    BestHold = h;
                }
            }

            //最高勝率と保有期間の表示
            Console.WriteLine();
            Console.WriteLine("最適株保有期間: " + BestHold.ToString());
            Console.WriteLine("最高勝率: " + (BestRate / stocks.Length).ToString());

            Console.ReadLine();
        }

        /// <summary>
        /// 過去の株価からカップ分析して勝率を計算する
        /// </summary>
        /// <param name="prices">過去の株価データ</param>
        /// <param name="minCup">カップ最低期間</param>
        /// <param name="maxCup">カップ最長期間</param>
        /// <param name="holdLength">保有期間</param>        /// <returns></returns>
        private static double GetWinningRate (List<HistoryPrice> prices, int minCup, int maxCup, int holdLength)
        {
            //まずはカップを取得する
            var cups = GetCups(prices, minCup, maxCup);

            //勝敗数の記録
            int win = 0;
            int lose = 0;

            //それぞれのカップの勝率を計算する
            foreach (var cup in cups)
            {
                //仮想株価上昇率を計算
                double hypGain = GetHypGain(cup.HighDate, cup.HighPrice, holdLength, prices);
                double hypGainRate = hypGain / cup.HighPrice * 100;
                //勝敗を記録
                if (hypGainRate >= 0) win++;
                else lose++;
            }
            //勝敗率を表示する
            Console.WriteLine("    " + win.ToString() + "/" + (win + lose).ToString());
            return (float)win / (win + lose);
        }

        
        /// <summary>
        /// カップ取得メソッド
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="cupduration"></param>
        /// <param name="buyduration"></param>
        /// <returns></returns>
        private static List<Cup> GetCups(List<HistoryPrice> prices, int minCup, int maxCup)
        {
            double highPrice = 0.0;
            DateTime highDate = new DateTime();
            double lowPrice = 100000000.0;
            int cupcounter = 0;
            int cupdays = 0;
            List<Cup> cups = new List<Cup>();

            foreach (HistoryPrice pricetoday in prices)
            {
                //もし新高値更新の場合
                if (pricetoday.Close > highPrice)
                {
                    //もしカップ最低期日を満たしていたらカップ登録
                    if (cupdays > minCup && cupdays < maxCup)
                    {
                        //新カップ登録
                        var newcup = new Cup();
                        newcup.StartDate = highDate;
                        newcup.StartPrice = highPrice;
                        newcup.BottomPrice = lowPrice;
                        newcup.HighDate = pricetoday.Date;
                        newcup.HighPrice = pricetoday.Close;
                        newcup.CupDays = cupdays;
                        newcup.CupID = cupcounter++;

                        cups.Add(newcup);

                        //カップカウンターを
                        cupcounter++;
                    }

                    //リセット
                    highPrice = pricetoday.Close;
                    highDate = pricetoday.Date;
                    lowPrice = 1000000;
                    cupdays = 0;
                }
                else
                {
                    //最安値だったら記録
                    if (lowPrice > pricetoday.Low)
                    {
                        lowPrice = pricetoday.Low;
                    }
                    
                    //カップ期日計算をインクリメント
                    cupdays++;
                }
            }
            return cups;
        }
        

        private static double GetHypGain(DateTime investDate, double purchasePrice, int holdDays, List<HistoryPrice> history)
        {
            double sellPrice = 0.0;
            foreach (var data in history)
            {
                //新値買いの日付からX日後となったらその日の値段を取る
                if (data.Date > investDate.AddDays(holdDays))
                {
                    //この日の値段が売り値
                    sellPrice = data.Close;
                    //差を返して終わり
                    return sellPrice - purchasePrice;
                }
            }
            return 0.0;
        }
    }

    /// <summary>
    /// Cupクラス
    /// </summary>
    class Cup
    {
        public int CupID; //カップの番号
        public int CupDays { get; set; } //カップの長さ
        public DateTime StartDate { get; set; } //カップ開始日
        public DateTime HighDate { get; set; } //カップ開始の株価
        public double StartPrice { get; set; } //高値更新日
        public double HighPrice { get; set; } //新高値
        public double BottomPrice { get; set; } //カップ期間中の最安値
    }

}
