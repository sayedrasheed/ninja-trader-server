using NinjaTrader.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenoh;

namespace NinjaTraderServer
{
    /// <summary>
    /// Class <c>OrderSubscriber</c> subscribes to Orders coming from TradeBot and then places them with
    /// the NinjaTrader API. It will then periodically check if this order is filled and when it is it will 
    /// publish OrderFilled to TradeBot
    /// </summary>
    internal class OrderSubscriber : ISubscribeCallback<Order>, IDisposable
    {
        private static readonly object ordersLock = new object();

        Client ninjaTrader; // NinjaTrader Client API
        Publisher<OrderFilled> orderFilledPublisher;

        private TimerCallback orderFillCallback;
        private System.Threading.Timer timerOrderFill;

        private Dictionary<string, Order> orders;
        private bool disposed;

        public SubscriberCallback<Order> OnData { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderSubscriber"/> class.
        /// </summary>
        /// <param name="orderFilledPublisher">Zenoh publisher to publish order fills to TradeBot</param>
        public OrderSubscriber(Publisher<OrderFilled> orderFilledPublisher)
        {
            this.orderFilledPublisher = orderFilledPublisher;

            ninjaTrader = new Client();
            int connect = ninjaTrader.Connected(0);
            Console.WriteLine(string.Format("{0} | connect: {1}", DateTime.Now, connect.ToString()));

            orderFillCallback = new TimerCallback(OrderFilledTimerElapsed);
            timerOrderFill = new System.Threading.Timer(orderFillCallback, null, 0, 100);

            orders = new Dictionary<string, Order>();
            OnData = OnDataOrderCallback;
        }

        /// <summary>
        /// Callback method when receiving the Order message. This method will place this order
        /// with the NinjaTrader API.
        /// </summary>
        /// <param name="orderFilledPublisher">Zenoh publisher to publish order fills to TradeBot</param>
        public void OnDataOrderCallback(Order message)
        {
            Console.WriteLine("Order message received");
            var contract = GetContractSymbol(message.Symbol, DateTime.UtcNow);
            for (int i = 0; i < message.AccountIds.Count; ++i)
            {
                Guid uuid = Guid.NewGuid();
                string uuidString = uuid.ToString();

                int result = ninjaTrader.Command(command: "PLACE", account: message.AccountIds[i], 
                    instrument: contract, 
                    action: message.Size > 0 ? "BUY" : "SELL", 
                    quantity: Math.Abs(message.Size), 
                    orderType: "MARKET", 
                    orderId: uuidString, 
                    timeInForce: "GTC", 
                    limitPrice: message.Price, 
                    stopPrice: 0.0, 
                    oco: uuidString, 
                    tpl: "", 
                    strategy: "");

                lock (ordersLock)
                {
                    orders.Add(uuidString, message);
                }
            }

        }

        /// <summary>
        /// Checks the current order list if the orders are filled. If they are it will publish OrderFilleds to TradeBot
        /// </summary>
        private void OrderFilledTimerElapsed(object? sender)
        {
            lock (ordersLock)
            {
                foreach (KeyValuePair<string, Order> entry in orders)
                {
                    int result = ninjaTrader.Filled(entry.Key);

                    if (result == Math.Abs(entry.Value.Size))
                    {
                        orderFilledPublisher.Publish(new OrderFilled { TimestampNs = GetCurrentTimeInNanoseconds(), 
                                                                       Symbol = entry.Value.Symbol, 
                                                                       StrategyId = entry.Value.StrategyId, 
                                                                       OrderId = entry.Value.OrderId, 
                                                                       OrderType = (OrderFilled.Types.OrderType)entry.Value.OrderType, 
                                                                       Price = (float)ninjaTrader.AvgFillPrice(entry.Key), 
                                                                       Size = entry.Value.Size });
                        orders.Remove(entry.Key);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                timerOrderFill.Dispose();
                disposed = true;
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

        /// <summary>
        /// Generate futures contract symbol using the symbol and the current date
        /// </summary>
        public static string GetContractSymbol(string symbol, DateTime dt)
        {
            var month = dt.ToString("MMMM", CultureInfo.InvariantCulture);
            var year = dt.Year;

            var secondFriday = GetSecondFriday(dt);

            string contractLetter = month switch
            {
                "January" => "H",
                "February" => "H",
                "March" => dt < secondFriday ? "H" : "M",
                "April" => "M",
                "May" => "M",
                "June" => dt < secondFriday ? "M" : "U",
                "July" => "U",
                "August" => "U",
                "September" => dt < secondFriday ? "U" : "Z",
                "October" => "Z",
                "November" => "Z",
                "December" => dt < secondFriday ? "Z" : "H",
                _ => throw new ArgumentException("Invalid month")
            };

            int contractYear = year % 10;
            return $"{symbol}{contractLetter}{contractYear}";
        }

        private static DateTime GetSecondFriday(DateTime dt)
        {
            var firstDayOfMonth = new DateTime(dt.Year, dt.Month, 1);
            var daysToAdd = ((int)DayOfWeek.Friday - (int)firstDayOfMonth.DayOfWeek + 7) % 7;
            var firstFriday = firstDayOfMonth.AddDays(daysToAdd);
            return firstFriday.AddDays(7); 
        }
    }
}
