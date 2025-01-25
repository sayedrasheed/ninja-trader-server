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
using Microsoft.VisualBasic.Devices;
using System.Security.Policy;
using System.Collections;
using System.Drawing.Drawing2D;
using Google.Protobuf.WellKnownTypes;

namespace NinjaTraderServer
{
    /// <summary>
    /// Class <c>ServiceSubscriber</c> Subscriber to the TradeBot service network. It subscribes to the
    /// StartNinjaDatafeed message and the StartNinjaOrderfeed message. These messages will tell this server
    /// where to publish the datafeed and orderfeed coming from the NinjaTrader API
    /// </summary>
    internal class ServiceSubscriber : ISubscribeCallback<StartNinjaDatafeed>, 
                                       ISubscribeCallback<StartNinjaOrderfeed>, 
                                       IDisposable
    {
        Dictionary<string, DatafeedSubscriber> datafeedMap;
        Dictionary<string, OrderSubscriber> orderMap;
        List<Node> nodes;
        private bool disposed;

        SubscriberCallback<StartNinjaDatafeed> ISubscribeCallback<StartNinjaDatafeed>.OnData { get; set; }
        SubscriberCallback<StartNinjaOrderfeed> ISubscribeCallback<StartNinjaOrderfeed>.OnData { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceSubscriber"/> class.
        /// </summary>
        public ServiceSubscriber()
        {
            datafeedMap = new Dictionary<string, DatafeedSubscriber>();
            orderMap = new Dictionary<string, OrderSubscriber>();
            nodes = new List<Node>();
            ((ISubscribeCallback<StartNinjaDatafeed>)this).OnData = OnDataStartNinjaDatafeed;
            ((ISubscribeCallback<StartNinjaOrderfeed>)this).OnData = OnDataStartNinjaOrderfeed;
        }

        /// <summary>
        /// Callback method when receiving the StartNinjaOrderfeed message. This method will
        /// get the network and topics from the message and set up its zenoh node with these 
        /// parameters to publish/subscribe to Orders and OrderFills
        /// </summary>
        /// <param name="message">StartNinjaOrderfeed message received</param>
        public void OnDataStartNinjaOrderfeed(StartNinjaOrderfeed message)
        {
            Console.WriteLine("StartNinjaOrderfeed message received");

            string runKey = $"{message.BatchId}-{message.StrategyId}";
            if (!orderMap.ContainsKey(runKey))
            {
                // Set up zenoh node with the requested network and topic list
                string network = $"'{message.Network.Ip}:{message.Network.Port}'";
                Console.WriteLine("Network: {0}", network);
                Config config = new Config(network);
                var node = new Node(config);
                Publisher<OrderFilled> orderFilledPublisher = node.NewPublisher<OrderFilled>("order_filled");
                string orderTopic = "order"; // use default topic unless message says otherwise

                for (int i = 0; i < message.Topics.Count; ++i)
                {
                    switch (message.Topics[i].Mtype)
                    {
                        case enums.Types.MessageType.OrderFilled:
                            Console.WriteLine("OrderFilled Topic: {0}", message.Topics[i].Topic_);
                            orderFilledPublisher = node.NewPublisher<OrderFilled>(message.Topics[i].Topic_);
                            break;
                        case enums.Types.MessageType.Order:
                            Console.WriteLine("Order Topic: {0}", message.Topics[i].Topic_);
                            orderTopic = message.Topics[i].Topic_;
                            break;
                        default:
                            break;
                    }
                }

                // Start subscriber and subscribe to Orders coming from the orderfeed
                Subscriber subscriber = node.NewSubscriber();
                OrderSubscriber orderSubscriber = new OrderSubscriber(orderFilledPublisher);
                subscriber.subscribe<Order>(orderTopic, orderSubscriber);

                // Save off for disposing later
                nodes.Add(node);
                orderMap.Add(runKey, orderSubscriber);
            }
        }

        /// <summary>
        /// Callback method when receiving the StartNinjaDatafeedfeed message. This method will
        /// get the network and topics from the message and set up its zenoh node with these 
        /// parameters to publish Ticks, Candles, and HistoricalData
        /// </summary>
        /// <param name="message">StartNinjaOrderfeed message received</param>
        public void OnDataStartNinjaDatafeed(StartNinjaDatafeed message)
        {
            Console.WriteLine("StartNinjaDatafeed message received");
            // TODO: Handle case where datafeed for same symbol is requested. For now just dont allow it,
            // this is fine for my current needs
            if (!datafeedMap.ContainsKey(message.Symbol))
            {
                // Set up zenoh node with the requested network and topic list
                string network = $"'{message.Network.Ip}:{message.Network.Port}'";
                Console.WriteLine("Network: {0}", network);
                Config config = new Config(network);
                var node = new Node(config);

                // use default topic for publishers unless message says otherwise
                Publisher<Tick> tickPublisher = node.NewPublisher<Tick>("tick");
                Publisher<Candle> candlePublisher = node.NewPublisher<Candle>("candle");
                Publisher<HistoricalData> historicalPublisher = node.NewPublisher<HistoricalData>("historical_data");

                for (int i = 0; i < message.Topics.Count; ++i)
                {
                    switch (message.Topics[i].Mtype)
                    {
                        case enums.Types.MessageType.Tick:
                            Console.WriteLine("Tick Topic: {0}", message.Topics[i].Topic_);
                            tickPublisher = node.NewPublisher<Tick>(message.Topics[i].Topic_);
                            break;
                        case enums.Types.MessageType.Candle:
                            Console.WriteLine("Candle: {0}", message.Topics[i].Topic_);
                            candlePublisher = node.NewPublisher<Candle>(message.Topics[i].Topic_);
                            break;
                        case enums.Types.MessageType.HistoricalData:
                            Console.WriteLine("HistoricalData: {0}", message.Topics[i].Topic_);
                            historicalPublisher = node.NewPublisher<HistoricalData>(message.Topics[i].Topic_);
                            break;
                        default:
                            break;
                    }
                }

                // Start subscriber and subscribe to NinjaTrader application to get the datafeed.
                DatafeedSubscriber datafeedSubscriber = new DatafeedSubscriber(message.Symbol, message.CandlePeriods.ToList(), tickPublisher, candlePublisher, historicalPublisher);

                // Save off for disposing later
                nodes.Add(node);
                datafeedMap.Add(message.Symbol, datafeedSubscriber);
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
                for (int i = 0; i < nodes.Count; i++) { nodes[i].Dispose(); }
                foreach (KeyValuePair<string, DatafeedSubscriber> entry in datafeedMap)
                {
                    entry.Value.Dispose(); 
                }
                foreach (KeyValuePair<string, OrderSubscriber> entry in orderMap)
                {
                    entry.Value.Dispose();
                }
                disposed = true;
            }

        }
    }
}
