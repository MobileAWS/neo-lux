﻿using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Neo.Lux.Cryptography;
using Neo.Lux.Utils;
using Neo.Lux.VM;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Neo.Lux.Emulator
{
    public class EmulatorServer
    {
        private Emulator emulator;
        private TcpListener server;

        public Dictionary<string, UInt160> scriptMap = new Dictionary<string, UInt160>();

        public EmulatorServer(Emulator emulator, int port)
        {
            this.emulator = emulator;

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            
            // TcpListener server = new TcpListener(port);
            server = new TcpListener(IPAddress.Any, port);
        }

        private string ExecuteRequest(string method, string arg)
        {
            var result = DataNode.CreateObject("response");

            switch (method)
            {
                case "GetChainHeight":
                    {
                        result.AddField("height", this.emulator.GetBlockHeight());
                        break;
                    }

                case "GetBlockByHeight":
                    {
                        var height = uint.Parse(arg);
                        var block = emulator.GetBlock(height);
                        var bytes = block.Serialize();
                        result.AddField("block", bytes.ByteToHex());
                        break;
                    }

                case "GetBlockByHash":
                    {
                        var hash = new UInt256(arg.HexToBytes());
                        var block = emulator.GetBlock(hash);
                        var bytes = block.Serialize();
                        result.AddField("block", bytes.ByteToHex());
                        break;
                    }

                case "GetContractHash":
                    {
                        var hash = this.scriptMap[arg];
                        result.AddField("address", hash.ToAddress());
                        break;
                    }

                case "GetAssetBalancesOf":
                    {
                        var assets = emulator.GetAssetBalancesOf(arg);
                        int index = 0;
                        foreach (var entry in assets)
                        {
                            var node = DataNode.CreateObject(index.ToString());
                            index++;
                            result.AddNode(node);

                            node.AddField("symbol", entry.Key);
                            node.AddField("value", entry.Value);
                        }
                        break;
                    }

                case "GetUnspent":
                    {
                        var unspents = emulator.GetUnspent(arg);
                        int index = 0;
                        foreach (var entry in unspents)
                        {
                            var node = DataNode.CreateObject(index.ToString());
                            index++;
                            result.AddNode(node);

                            node.AddField("asset", entry.Key);

                            var list = DataNode.CreateArray("unspents");
                            node.AddNode(list);

                            foreach (var temp in entry.Value)
                            {
                                var child = DataNode.CreateObject();
                                list.AddNode(child);

                                child.AddField("hash", temp.hash.ToArray().ByteToHex());
                                child.AddField("index", temp.index);
                                child.AddField("value", temp.value);
                            }
                        }
                        break;
                    }

                case "SendTransaction":
                    {
                        var bytes = arg.HexToBytes();
                        var success = emulator.SendRawTransaction(bytes);
                        result.AddField("success", success);
                        break;
                    }

                case "InvokeScript":
                    {
                        var script = arg.HexToBytes();

                        var obj = emulator.InvokeScript(script);

                        using (var wstream = new MemoryStream())
                        {
                            using (var writer = new BinaryWriter(wstream))
                            {
                                Serialization.SerializeStackItem(obj.result, writer);

                                var hex = wstream.ToArray().ByteToHex();

                                result.AddField("state", obj.state);
                                result.AddField("gas", obj.gasSpent);
                                result.AddField("stack", hex);
                            }
                        }

                        break;
                    }

                default:
                    {
                        result.AddField("error", "invalid method");
                        break;
                    }
            }

            var json = JSONWriter.WriteToString(result);
            return json;
        }

        public void Start() { 

            // Start listening for client requests.
            server.Start();

            // Buffer for reading data
            Byte[] bytes = new Byte[1024*64];

            // Enter the listening loop.
            while (true)
            {
                try
                {
                    Console.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    // Get a stream object for reading and writing
                    var stream = client.GetStream();

                    string input;

                    using (var reader = new StreamReader(stream))
                    {
                        input = reader.ReadLine();

                        Console.WriteLine(input);

                        var temp = input.Split('/');
                        var method = temp[0];
                        var val = temp[1];

                        string json;

                        lock (this)
                        {
                            json = ExecuteRequest(method, val);
                        }

                        Console.WriteLine(json);

                        var output = Encoding.UTF8.GetBytes(json);
                        stream.Write(output, 0, output.Length);
                        stream.Flush();
                        stream.Close();
                        client.Close();
                    }

                }
                catch (SocketException e)
                {
                    Console.WriteLine("SocketException: {0}", e);
                }
                
            }
           // Stop listening for new clients.
           server.Stop();
        }
    }
}
