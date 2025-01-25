using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenoh;
using Google.Protobuf;
using NinjaTrader.Client;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using Google.Protobuf.Collections;
using System.Timers;
using System.Diagnostics;

namespace NinjaTraderServer
{
    /// <summary>
    /// Class <c>DatafeedSubscriber</c> subscribes to Datafeed coming from NinjaTrader API. It will then convert this datafeed into
    /// TradeBot Protobuf messages and publish them to TradeBot
    /// </summary>
    internal class DatafeedSubscriber : IDisposable
    {
        string symbol;
        CandleGenerator candleGenerator;
        Client ninjaTrader;
        Publisher<Tick> tickPublisher;
        Publisher<Candle> candlePublisher;

        private TimerCallback marketDataCallback;
        private System.Threading.Timer timerMarketData;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatafeedSubscriber"/> class.
        /// </summary>
        /// <param name="symbol">Symbol for this datafeed</param>
        /// <param name="candlePeriods">Desired candle periods for this datafeed</param>
        /// <param name="tickPublisher">Tick publisher</param>
        /// <param name="candlePublisher">Candle publisher</param>
        /// <param name="historicalPublisher">Historical data publisher</param>
        public DatafeedSubscriber(string symbol, List<uint> candlePeriods, Publisher<Tick> tickPublisher, Publisher<Candle> candlePublisher, Publisher<HistoricalData> historicalPublisher)
        {
            this.symbol = symbol;
            this.tickPublisher = tickPublisher;
            this.candlePublisher = candlePublisher;

            for (int i = 0; i < candlePeriods.Count; ++i)
            {
                HistoricalData hd = new HistoricalData { Symbol = symbol, PeriodS = candlePeriods[i], TimestampNs = GetCurrentTimeInNanoseconds() };
                historicalPublisher.Publish(hd);
            }

            // NinjaTrader API only gives ticks so need to accumlate own candles so use CandleGenerator class
            candleGenerator = new CandleGenerator(symbol, candlePeriods);

            ninjaTrader = new Client(); // NinjaTrader Client API
            int connect = ninjaTrader.Connected(0);
            Console.WriteLine(string.Format("{0} | connect: {1}", DateTime.Now, connect.ToString()));

            // Subscribe to market data from NinjaTrader API for this symbol
            ninjaTrader.SubscribeMarketData(symbol);

            // Set up timer to periodically check the market data
            marketDataCallback = new TimerCallback(MarketDataTimerElapsed);
            timerMarketData = new System.Threading.Timer(marketDataCallback, null, 0, 100);
        }

        /// <summary>
        /// Checks the current price for the this symbol
        /// </summary>
        private void MarketDataTimerElapsed(object? sender)
        {
            float tickValue = (float)ninjaTrader.MarketData(symbol, 0);

            Console.WriteLine("TimestampNs: {0}, tickValue: {1}", GetCurrentTimeInNanoseconds(), tickValue);
            Tick tick = new Tick { TimestampNs = GetCurrentTimeInNanoseconds(), Symbol = this.symbol, Price = tickValue, Size = 0, Side = Tick.Types.Side.None };
            
            // Publish tick to TradeBot
            tickPublisher.Publish(tick);

            // Generate candles using the generated Tick. If candle is closed then publish to TradeBot
            List<Candle> closedCandles = candleGenerator.Tick(tick);
            for (int i = 0; i < closedCandles.Count; i++)
            {
                candlePublisher.Publish(closedCandles[i]);
            }
        }

        static ulong GetCurrentTimeInNanoseconds()
        {
            DateTime utcNow = DateTime.UtcNow;

            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            TimeSpan timeSpan = utcNow - unixEpoch;

            ulong unixTimestampInMicroseconds = (ulong)(timeSpan.Ticks / 10);
        
            return unixTimestampInMicroseconds * 1000;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                ninjaTrader.UnsubscribeMarketData(symbol);
                timerMarketData.Dispose();
                disposed = true;
            }
        }
    }
}
