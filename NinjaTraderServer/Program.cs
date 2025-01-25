using Zenoh;
using Google.Protobuf;
using NinjaTrader.Client;
using NinjaTraderServer;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;


class Program
{
    private static AutoResetEvent _exitEvent = new AutoResetEvent(false);
    static void Main(string[] args)
    {
        // Check if the file path argument is provided
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide the path to the YAML file as a command-line argument.");
            return;
        }

        string serviceYamlFile = args[0];

        // Check if the file exists
        if (!File.Exists(serviceYamlFile))
        {
            Console.WriteLine($"The file '{serviceYamlFile}' does not exist.");
            return;
        }

        // Read the YAML file content
        string yamlStr = File.ReadAllText(serviceYamlFile);
        
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        try
        {
            // Deserialize the YAML content into ServiceConfig
            var serviceConfig = deserializer.Deserialize<ServiceConfig>(yamlStr);

            // Get service topics
            var topicConfig = new TopicConfig(serviceConfig.topics);

            // Get Service IP and Port
            Console.WriteLine($"IP: {serviceConfig.ip}");
            Console.WriteLine($"port: {serviceConfig.port}");

            // Create node using service network
            string network = $"'{serviceConfig.ip}:{serviceConfig.port}'";
            Config config = new Config(network);
            var node = new Node(config);

            // Start service subscriber and subscribe to required messages
            ServiceSubscriber serviceSubscriber = new ServiceSubscriber();
            Subscriber subscriber = node.NewSubscriber();
            subscriber.subscribe<StartNinjaDatafeed>(topicConfig.get("start_ninja_datafeed"), serviceSubscriber);
            subscriber.subscribe<StartNinjaOrderfeed>(topicConfig.get("start_ninja_orderfeed"), serviceSubscriber);

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Ctrl+C pressed. Signaling to exit...");
                _exitEvent.Set();
                e.Cancel = true;
            };

            Console.WriteLine("Press Ctrl+C to stop.");

            _exitEvent.WaitOne();

            Console.WriteLine("Exit");
            serviceSubscriber.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to parse YAML file. {ex.Message}");
        }
    }
}

// Service yaml config
public class ServiceConfig
{
    public Dictionary<string,string> topics { get; set; }
    public string ip { get; set; }
    public uint port { get; set; }
}

// Topic config, same implementation as the one in TradeBot
public class TopicConfig
{
    public Dictionary<string, string> topics { get; set; }

    public TopicConfig(Dictionary<string, string> topics)
    { 
        this.topics = topics; 
    }

    public string get(string topic)
    {
        if (topics.ContainsKey(topic))
        {
            return topics[topic];
        }
        else
        {
            return topic;
        }
    }
}
