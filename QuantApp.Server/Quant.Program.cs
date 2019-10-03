﻿/*
 * The MIT License (MIT)
 * Copyright (c) 2007-2019, Arturo Rodriguez All rights reserved.
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using System.Reflection;

using QuantApp.Kernel;
using QuantApp.Kernel.Adapters.SQL;
using QuantApp.Engine;

using QuantApp.Server.Utils;

using Python.Runtime;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using AQI.AQILabs.Kernel;

namespace QuantApp.Server.Quant
{
    public class Program
    {
        private static string workspace_name = null;
        private static string hostName = null;
        private static string ssl_cert = null;
        private static string ssl_password = null;

        private static bool useJupyter = false;

        private static readonly System.Threading.AutoResetEvent _closing = new System.Threading.AutoResetEvent(false);
        public static void Main(string[] args)
        {
            #if NETCOREAPP3_0
            Console.Write("NetCoreApp 3.0... ");
            #endif

            #if NET461
            Console.Write("Net Framework 461... ");
            #endif
            
            Console.Write("Python starting... ");
            PythonEngine.Initialize();

            Code.InitializeCodeTypes(new Type[]{ 
                typeof(QuantApp.Engine.WorkSpace),
                typeof(Jint.Native.Array.ArrayConstructor),
                typeof(AQI.AQILabs.Kernel.Instrument), 
                typeof(AQI.AQILabs.Derivatives.CashFlow), 
                typeof(AQI.AQILabs.SDK.Strategies.PortfolioStrategy)
                });

            JObject config = (JObject)JToken.ReadFrom(new JsonTextReader(File.OpenText(@"mnt/quantapp_config.json")));
            workspace_name = config["Workspace"].ToString();
            hostName = config["Server"]["Host"].ToString();
            var secretKey = config["Server"]["SecretKey"].ToString();
            ssl_cert = config["Server"]["SSL"]["Cert"].ToString();
            ssl_password = config["Server"]["SSL"]["Password"].ToString();
            var sslFlag = !string.IsNullOrWhiteSpace(ssl_cert);

            var cloudHost = config["Cloud"]["Host"].ToString();
            var cloudKey = config["Cloud"]["SecretKey"].ToString();
            var cloudSSL = config["Cloud"]["SSL"].ToString();

            useJupyter = config["Jupyter"].ToString().ToLower() == "true";

            if(args != null && args.Length > 0 && args[0] == "lab")
            {
                Connection.Client.Init(hostName, sslFlag);

                if(!Connection.Client.Login(secretKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();
                
                var pargs = new string[] {"-m", "ipykernel_launcher.py", "-f", args[1] };
                Console.Write("Starting lab... ");

                Python.Runtime.Runtime.Py_Main(pargs.Length, pargs);
                Console.WriteLine("started lab... ");

            }
            //Cloud
            else if(args != null && args.Length > 1 && args[0] == "cloud" && args[1] == "deploy")
            {
                Console.WriteLine("Cloud Host: " + cloudHost);
                Console.WriteLine("Cloud SSL: " + cloudSSL);
                Connection.Client.Init(cloudHost, cloudSSL.ToLower() == "true");

                if(!Connection.Client.Login(cloudKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();

                Console.Write("Starting cloud deployment... ");

                Code.UpdatePackageFile(workspace_name);
                var t0 = DateTime.Now;
                Console.WriteLine("Started: " + t0);
                var res = Connection.Client.PublishPackage(workspace_name);
                var t1 = DateTime.Now;
                Console.WriteLine("Ended: " + t1 + " taking " + (t1 - t0));
                Console.Write("Result: " + res);
            }
            else if(args != null && args.Length > 1 && args[0] == "cloud" && args[1] == "build")
            {
                Console.WriteLine("Cloud Host: " + cloudHost);
                Console.WriteLine("Cloud SSL: " + cloudSSL);
                Connection.Client.Init(cloudHost, cloudSSL.ToLower() == "true");

                if(!Connection.Client.Login(cloudKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();

                Console.Write("CoFlows Cloud build... ");

                Code.UpdatePackageFile(workspace_name);
                var t0 = DateTime.Now;
                Console.WriteLine("Started: " + t0);
                var res = Connection.Client.BuildPackage(workspace_name);
                var t1 = DateTime.Now;
                Console.WriteLine("Ended: " + t1 + " taking " + (t1 - t0));
                Console.Write("Result: " + res);
            }
            else if(args != null && args.Length > 2 && args[0] == "cloud" && args[1] == "query")
            {
                Console.WriteLine("Cloud Host: " + cloudHost);
                Console.WriteLine("Cloud SSL: " + cloudSSL);
                Connection.Client.Init(cloudHost, cloudSSL.ToLower() == "true");

                if(!Connection.Client.Login(cloudKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();

                Console.WriteLine("CoFlows Cloud query... ");

                var queryID = args[2];
                var funcName = args.Length > 3 ? args[3] : null;
                var parameters = args.Length > 4 ? args.Skip(4).ToArray() : null;

                var pkg = Code.ProcessPackageFile(workspace_name);
                Console.WriteLine("Workspace: " + pkg.Name);

                Console.WriteLine("Query ID: " + queryID);
                Console.WriteLine("Function Name: " + funcName);
                
                if(parameters != null)
                    for(int i = 0; i < parameters.Length; i++)
                        Console.WriteLine("Parameter[" + i + "]: " + parameters[i]);

                
                var (code_name, code) = pkg.Queries.Where(entry => entry.ID == queryID).Select(entry => (entry.Name as string, entry.Content as string)).FirstOrDefault();

                var t0 = DateTime.Now;
                Console.WriteLine("Started: " + t0);
                var result = Connection.Client.Execute(code, code_name, pkg.ID, queryID, funcName, parameters);
                var t1 = DateTime.Now;
                Console.WriteLine("Ended: " + t1 + " taking " + (t1 - t0));
                
                Console.WriteLine("Result: ");
                Console.WriteLine(result);
            }
            else if(args != null && args.Length > 1 && args[0] == "cloud" && args[1] == "log")
            {
                Console.WriteLine("Cloud Host: " + cloudHost);
                Console.WriteLine("Cloud SSL: " + cloudSSL);
                Connection.Client.Init(cloudHost, cloudSSL.ToLower() == "true");

                if(!Connection.Client.Login(cloudKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();

                Console.Write("CoFlows Cloud log... ");

                var res = Connection.Client.RemoteLog(workspace_name);
                Console.WriteLine("Result: ");
                Console.WriteLine(res);
            }
            else if(args != null && args.Length > 1 && args[0] == "cloud" && args[1] == "remove")
            {
                Console.WriteLine("Cloud Host: " + cloudHost);
                Console.WriteLine("Cloud SSL: " + cloudSSL);
                Connection.Client.Init(cloudHost, cloudSSL.ToLower() == "true");

                if(!Connection.Client.Login(cloudKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();

                Console.Write("CoFlows Cloud log... ");

                var res = Connection.Client.RemoteRemove(workspace_name);
                Console.WriteLine("Result: ");
                Console.WriteLine(res);
            }
            else if(args != null && args.Length > 1 && args[0] == "cloud" && args[1] == "restart")
            {
                Console.WriteLine("Cloud Host: " + cloudHost);
                Console.WriteLine("Cloud SSL: " + cloudSSL);
                Connection.Client.Init(cloudHost, cloudSSL.ToLower() == "true");

                if(!Connection.Client.Login(cloudKey))
                    throw new Exception("CoFlows Not connected!");

                Connection.Client.Connect();
                Console.Write("server connected! ");

                QuantApp.Kernel.M.Factory = new MFactory();

                Console.Write("CoFlows Cloud log... ");

                var res = Connection.Client.RemoteRestart(workspace_name);
                Console.WriteLine("Result: ");
                Console.WriteLine(res);
            }
            else if(args != null && args.Length > 0 && args[0] == "server")
            {
                PythonEngine.BeginAllowThreads();

                if(config["Database"].Children().Count() > 0)
                {
                    var kernelString = config["Database"]["Kernel"].ToString();
                    var strategyString = config["Database"]["Strategies"].ToString();
                    var quantappString = config["Database"]["QuantApp"].ToString();
                    Databases(kernelString, strategyString, quantappString);
                }
                else
                {
                    var connectionString = config["Database"].ToString();
                    Databases(connectionString);
                }

                SetRTD();

                Console.WriteLine("QuantApp Server " + DateTime.Now);
                Console.WriteLine("DB Connected");

                Console.WriteLine("Local deployment");

                var pkg = Code.ProcessPackageFile(workspace_name);
                Code.ProcessPackageJSON(pkg);
                SetDefaultWorkSpaces(new string[]{ pkg.ID });

                /// QuantSpecific START
                Instrument.TimeSeriesLoadFromDatabaseIntraday = config["Quant"]["Intraday"].ToString().ToLower() == "true";
                if(Instrument.TimeSeriesLoadFromDatabaseIntraday)
                    Console.WriteLine("Intraday Timeseries");
                else
                    Console.WriteLine("Close Timeseries");
                Strategy.Executer = true;
                // Market.Initialize();

                var saveAll = config["Quant"]["AutoSave"].ToString().ToLower() == "true";
                if (saveAll) {
                    var ths = new System.Threading.Thread(x => (AQI.AQILabs.Kernel.Instrument.Factory as AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLInstrumentFactory).SaveAllLoop(5));
                    ths.Start();
                }
                else
                    Console.WriteLine("Not saving timeseries");
                /// QuantSpecific END

                
                #if NETCOREAPP3_0
                if(!sslFlag)
                    QuantApp.Server.Program.Init(new string[]{"--urls", "http://*:80"});
                else
                    QuantApp.Server.Program.Init(args);
                #endif

                #if NET461
                QuantApp.Server.Program.Init(new string[]{"--urls", "http://*:80"});
                #endif
            
            
                Task.Factory.StartNew(() => {
                    while (true)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                });
                Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);
                _closing.WaitOne();
            }
            else
                Console.WriteLine("Wrong argument");

        }

        protected static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("bye bye!");
            _closing.Set();
            Environment.Exit(0);
        }

        private static void SetRTD()
        {
            QuantApp.Server.Realtime.WebSocketListner.RTDMessageFunction = new QuantApp.Server.Realtime.RTDMessageDelegate((message_string) => {
                var message = JsonConvert.DeserializeObject<QuantApp.Kernel.RTDMessage>(message_string);
                if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.MarketData)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.MarketData content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.MarketData>(message.Content.ToString());

                        AQI.AQILabs.Kernel.Instrument instrument = AQI.AQILabs.Kernel.Instrument.FindInstrument(content.InstrumentID);

                        DateTime stamp = content.Timestamp;
                        if (content.Value != 0)
                            instrument.AddTimeSeriesPoint(stamp, content.Value, content.Type, AQI.AQILabs.Kernel.DataProvider.DefaultProvider, true, false);

                        return Tuple.Create(instrument.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("MarketData Exception: " + e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.StrategyData)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.StrategyData content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.StrategyData>(message.Content.ToString());

                        AQI.AQILabs.Kernel.Strategy instrument = AQI.AQILabs.Kernel.Instrument.FindInstrument(content.InstrumentID) as AQI.AQILabs.Kernel.Strategy;

                        instrument.AddMemoryPoint(content.Timestamp, content.Value, content.MemoryTypeID, content.MemoryClassID, true, false);

                        return Tuple.Create(instrument.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("StrategyData Exception: " + e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.CreateAccount)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.CreateAccount content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.CreateAccount>(message.Content.ToString());

                        int id = content.StrategyID;

                        AQI.AQILabs.Kernel.Strategy s = AQI.AQILabs.Kernel.Instrument.FindInstrument(id) as AQI.AQILabs.Kernel.Strategy;
                        if (s != null)
                        {
                            QuantApp.Kernel.User user = QuantApp.Kernel.User.FindUser(content.UserID);
                            QuantApp.Kernel.User attorney = content.AttorneyID == null ? null : QuantApp.Kernel.User.FindUser(content.AttorneyID);
                            if (user != null && attorney != null)
                            {
                                List<PALMPending> pendings = PALM.GetPending(user);
                                foreach (PALMPending pending in pendings)
                                {
                                    if (pending.AccountID == content.AccountID)
                                    {

                                        pending.Strategy = s;
                                        pending.CreationDate = s.CreateTime;
                                        pending.Attorney = attorney;
                                        PALM.UpdatePending(pending);
                                        PALM.AddStrategy(pending.User, pending.Attorney, s);
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Create Account Exception: " + e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.UpdateOrder)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.OrderMessage content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.OrderMessage>(message.Content.ToString());

                        content.Order.Portfolio.UpdateOrder(content.Order, content.OnlyMemory, false);

                        return Tuple.Create(content.Order.Portfolio.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("UpdateOrder Exception: " + e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.UpdatePosition)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.PositionMessage content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.PositionMessage>(message.Content.ToString());

                        content.Position.Portfolio.UpdatePositionMemory(content.Position, content.Timestamp, content.AddNew, true, false);

                        return Tuple.Create(content.Position.Portfolio.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("UpdatePosition Exception: " + e + " --- " + e.StackTrace);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.AddNewOrder)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.Order content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.Order>(message.Content.ToString());

                        content.Portfolio.AddOrderMemory(content);

                        return Tuple.Create(content.Portfolio.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("AddNewOrder Exception: " + e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.AddNewPosition)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.Position content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.Position>(message.Content.ToString());

                        content.Portfolio.UpdatePositionMemory(content, content.Timestamp, true, true, false);

                        return Tuple.Create(content.Portfolio.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("AddNewPosition Exception: " + e + " " + e.StackTrace);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.SavePortfolio)
                {
                    try
                    {
                        int content = JsonConvert.DeserializeObject<int>(message.Content.ToString());

                        (AQI.AQILabs.Kernel.Instrument.FindInstrument(content) as AQI.AQILabs.Kernel.Portfolio).SaveNewPositions();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.Property)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.PropertyMessage content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.PropertyMessage>(message.Content.ToString());

                        object obj = AQI.AQILabs.Kernel.Instrument.FindInstrument(content.ID);

                        PropertyInfo prop = obj.GetType().GetProperty(content.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (null != prop && prop.CanWrite)
                        {
                            if (content.Value.GetType() == typeof(Int64))
                                prop.SetValue(obj, Convert.ToInt32(content.Value), null);
                            else
                                prop.SetValue(obj, content.Value, null);
                        }


                        AQI.AQILabs.Kernel.Instrument instrument = obj as AQI.AQILabs.Kernel.Instrument;
                        return Tuple.Create(instrument.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                else if ((int)message.Type == (int)AQI.AQILabs.Kernel.RTDMessage.MessageType.Function)
                {
                    try
                    {
                        AQI.AQILabs.Kernel.RTDMessage.FunctionMessage content = JsonConvert.DeserializeObject<AQI.AQILabs.Kernel.RTDMessage.FunctionMessage>(message.Content.ToString());
                        object obj = AQI.AQILabs.Kernel.Instrument.FindInstrument(content.ID);

                        MethodInfo method = obj.GetType().GetMethod(content.Name);
                        if (null != method)
                            method.Invoke(obj, content.Parameters);

                        AQI.AQILabs.Kernel.Instrument instrument = obj as AQI.AQILabs.Kernel.Instrument;

                        return Tuple.Create(instrument.ID.ToString(), message_string);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                return null;
            });
        }

        private static void Databases(string sqliteFile)
        {
            string KernelConnectString = "Data Source=" + sqliteFile;
            bool dbExists = File.Exists(sqliteFile);

            string CloudAppConnectString = KernelConnectString;
            string StrategyConnectString = KernelConnectString;

            if(!dbExists)
            {
                SQLiteDataSetAdapter KernelDataAdapter = new SQLiteDataSetAdapter();
                KernelDataAdapter.ConnectString = KernelConnectString;
                QuantApp.Kernel.Database.DB.Add("Kernel", KernelDataAdapter);
                Console.WriteLine("Creating table structure in: " + sqliteFile);
                var script = File.ReadAllText(@"create.sql");
                QuantApp.Kernel.Database.DB["Kernel"].ExecuteCommand(script);
            }

            Databases(KernelConnectString, StrategyConnectString, CloudAppConnectString);
        }
        private static void Databases(string KernelConnectString, string StrategyConnectString, string CloudAppConnectString)
        {
            if (QuantApp.Kernel.User.CurrentUser == null)
                QuantApp.Kernel.User.CurrentUser = new QuantApp.Kernel.User("System");

            if (QuantApp.Kernel.M.Factory == null)
            {         
                if(KernelConnectString.StartsWith("Server="))
                {
                    MSSQLDataSetAdapter KernelDataAdapter = new MSSQLDataSetAdapter();
                    KernelDataAdapter.ConnectString = KernelConnectString;
                    QuantApp.Kernel.Database.DB.Add("Kernel", KernelDataAdapter);
                }
                else
                {
                    if(!QuantApp.Kernel.Database.DB.ContainsKey("Kernel"))
                    {
                        SQLiteDataSetAdapter KernelDataAdapter = new SQLiteDataSetAdapter();
                        KernelDataAdapter.ConnectString = KernelConnectString;
                        QuantApp.Kernel.Database.DB.Add("Kernel", KernelDataAdapter);
                    }
                }


                QuantApp.Kernel.M.Factory = new QuantApp.Kernel.Adapters.SQL.Factories.SQLMFactory();


                // Quant
                Calendar.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLCalendarFactory();
                Currency.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLCurrencyFactory();
                CurrencyPair.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLCurrencyPairFactory();
                DataProvider.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLDataProviderFactory();
                Exchange.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLExchangeFactory();

                Instrument.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLInstrumentFactory();
                Security.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLSecurityFactory();
                Future.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLFutureFactory();
                Portfolio.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLPortfolioFactory();
                Strategy.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLStrategyFactory();
                Market.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLMarketFactory();

                InterestRate.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLInterestRateFactory();
                Deposit.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLDepositFactory();
                InterestRateSwap.Factory = new AQI.AQILabs.Kernel.Adapters.SQL.Factories.SQLInterestRateSwapFactory();

                DataProvider.DefaultProvider = DataProvider.FindDataProvider("AQI");

            }

            if (!QuantApp.Kernel.Database.DB.ContainsKey("CloudApp"))
            {
                if(CloudAppConnectString.StartsWith("Server="))
                {
                    if(KernelConnectString == CloudAppConnectString)
                        QuantApp.Kernel.Database.DB.Add("CloudApp", QuantApp.Kernel.Database.DB["Kernel"]);
                    else
                    {
                        MSSQLDataSetAdapter CloudAppDataAdapter = new MSSQLDataSetAdapter();
                        CloudAppDataAdapter.ConnectString = CloudAppConnectString;
                        QuantApp.Kernel.Database.DB.Add("CloudApp", CloudAppDataAdapter);
                    }
                }
                else
                {
                    if(KernelConnectString == CloudAppConnectString)
                        QuantApp.Kernel.Database.DB.Add("CloudApp", QuantApp.Kernel.Database.DB["Kernel"]);
                    else
                    {
                        SQLiteDataSetAdapter CloudAppDataAdapter = new SQLiteDataSetAdapter();
                        CloudAppDataAdapter.ConnectString = CloudAppConnectString;
                        QuantApp.Kernel.Database.DB.Add("CloudApp", CloudAppDataAdapter);
                    }
                }

                QuantApp.Kernel.User.Factory = new QuantApp.Kernel.Adapters.SQL.Factories.SQLUserFactory();
                Group.Factory = new QuantApp.Kernel.Adapters.SQL.Factories.SQLGroupFactory();
            }

            if (!QuantApp.Kernel.Database.DB.ContainsKey("DefaultStrategy"))
            {
                if(CloudAppConnectString.StartsWith("Server="))
                {
                    if(KernelConnectString == StrategyConnectString)
                        QuantApp.Kernel.Database.DB.Add("DefaultStrategy", QuantApp.Kernel.Database.DB["Kernel"]);
                    else
                    {
                        MSSQLDataSetAdapter StrategyDataAdapter = new MSSQLDataSetAdapter();
                        StrategyDataAdapter.ConnectString = StrategyConnectString;
                        QuantApp.Kernel.Database.DB.Add("DefaultStrategy", StrategyDataAdapter);
                    }
                }
                else
                {
                    if(KernelConnectString == StrategyConnectString)
                        QuantApp.Kernel.Database.DB.Add("DefaultStrategy", QuantApp.Kernel.Database.DB["Kernel"]);
                    else
                    {
                        SQLiteDataSetAdapter StrategyDataAdapter = new SQLiteDataSetAdapter();
                        StrategyDataAdapter.ConnectString = StrategyConnectString;
                        QuantApp.Kernel.Database.DB.Add("DefaultStrategy", StrategyDataAdapter);
                    }
                }
            }
        }

        public static IEnumerable<WorkSpace> SetDefaultWorkSpaces(string[] ids)
        {
            return QuantApp.Server.Program.SetDefaultWorkSpaces(ids);
        }


        public static IEnumerable<WorkSpace> GetDefaultWorkSpaces()
        {
            return QuantApp.Server.Program.GetDefaultWorkSpaces();
        }

        public static void AddServicedWorkSpaces(string id)
        {
            QuantApp.Server.Program.AddServicedWorkSpaces(id);
        }

        public static IEnumerable<string> GetServicedWorkSpaces()
        {
            return QuantApp.Server.Program.GetServicedWorkSpaces();
        }
    }
}