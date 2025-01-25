using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTraderServer
{
    /// <summary>
    /// Class <c>CandleGenerator</c> accumulates candles and generates them once they are closed.
    /// </summary>
    internal class CandleGenerator
    {
        private Dictionary<uint, Candle> currentCandles;
        public CandleGenerator(string symbol, List<uint> candlePeriods) 
        {
            currentCandles = new Dictionary<uint, Candle>();
            for (int i = 0; i < candlePeriods.Count; i++)
            {
                currentCandles.Add(candlePeriods[i], new Candle { PeriodS = candlePeriods[i], Symbol = symbol, Ohlcv = new Ohlcv() });
            }
        }

        /// <summary>
        /// Updates candles based on the Tick passed in
        /// </summary>
        /// <param name="tick">Current tick</param>
        /// <returns>
        /// A List of the candles that we closed from this Tick
        /// </returns>
        public List<Candle> Tick(Tick tick)
        {
            List<Candle> closedCandles = new List<Candle>();

            Console.WriteLine("Tick, timestamp: {0}, price: {1}", tick.TimestampNs, tick.Price);
            foreach (KeyValuePair<uint, Candle> entry in currentCandles)
            {
                var periodSeconds = entry.Key;
                // Candles in the dict are initialized to 0 so if first candle
                if (entry.Value.Ohlcv.TimestampNs == 0)
                {
                    // Ohlcv timestamps are timestamp when candle closed so we need to round up to nearest period
                    entry.Value.Ohlcv.TimestampNs = RoundUpToNearestPeriod(tick.TimestampNs, periodSeconds);
                    entry.Value.Ohlcv.Open = tick.Price;
                    entry.Value.Ohlcv.High = tick.Price;
                    entry.Value.Ohlcv.Low = tick.Price;
                    entry.Value.Ohlcv.Close = tick.Price;
                    entry.Value.Ohlcv.Volume = tick.Size;
                }
                else
                {
                    // If tick timestamp is >= to ohclv timestamp then that means candle needs to be closed
                    if(tick.TimestampNs >= entry.Value.Ohlcv.TimestampNs)
                    {
                        // Add candle to closed candle list so caller knows candle was closed
                        closedCandles.Add(entry.Value.Clone());

                        // Update to next candle
                        entry.Value.Ohlcv.TimestampNs += (ulong)periodSeconds * 1000000000;
                        entry.Value.Ohlcv.Open = tick.Price;
                        entry.Value.Ohlcv.High = tick.Price;
                        entry.Value.Ohlcv.Low = tick.Price;
                        entry.Value.Ohlcv.Close = tick.Price;
                        entry.Value.Ohlcv.Volume = tick.Size;
                    }
                    // Else we are still in the same candle so just update 
                    else
                    {
                        entry.Value.Ohlcv.Close = tick.Price;
                        entry.Value.Ohlcv.High = Math.Max(entry.Value.Ohlcv.High, tick.Price);
                        entry.Value.Ohlcv.Low = Math.Min(entry.Value.Ohlcv.Low, tick.Price);
                        entry.Value.Ohlcv.Volume += tick.Size;
                    }
                }
            }

            return closedCandles;
        }

        public static ulong RoundUpToNearestPeriod(ulong timestampNs, uint periodSeconds)
        {
            // Convert the period to nanoseconds
            ulong periodNs = (ulong)periodSeconds * 1000000000;

            // Round up to the nearest period
            ulong remainder = timestampNs % periodNs;
            if (remainder > 0)
            {
                timestampNs -= remainder;
            }

            return timestampNs;
        }
    }
}
