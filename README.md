# Ninja Trader Server
This server application integrates with the NinjaTrader Client API DLL to retrieve real-time market data, place orders, and interact with an external TradeBot application to automate trading strategies. It communicates with TradeBot via the Zenoh protocol, handling requests to initiate real-time data feeds and listens for order messages to execute trades.

# Installation
NinjaTrader only offers a .NET DLL to use their API so this application needed to be a .NET application that can communicate using the Zenoh protocol. There is limited support for the Zenoh protocol in C# so I created a library for ease of use. This can be found here:

https://github.com/sayedrasheed/zenoh-node-csharp

This library is added as a submodule which this VS solution will compile. 

1. Install [Visual Studio](https://visualstudio.microsoft.com/downloads/)
2. The Zenoh C API version 0.10.0-rc needs to be installed. Download the release here: [zenohc-0.10.0-rc](https://github.com/eclipse-zenoh/zenoh-c/releases/download/0.10.0-rc/zenoh-c-0.10.0-rc-x86_64-pc-windows-msvc.zip)
3. Extract zip
4. There should be a DLL in the extracted zip. Add that path to your PATH environment variable
5. Install [Protobuf](https://github.com/protocolbuffers/protobuf/releases/tag/v3.16.0) NOTE: Needs to be version >=3.16.0
6. Clone the repo and run git submodule update --init --recursive
7. Open solution in Visual Studio to build and run

# Demo
