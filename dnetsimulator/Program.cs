using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace DotnetIotSimulator
{
    ///Sample IoT Hub Client simulator app

    class Program
    {

        static int Interval { get; set; } = 3000;
        static double minTemperature = 20;
        static double minHumidity = 60;
        static double minPressure = 500;

        static DeviceClient deviceClient;
        static CancellationTokenSource cToken;
        static TwinCollection reportedProperties = new TwinCollection();

        static string iotDeviceConnectionString = ""; //supplied in command line argument
        static string simulatedDeviceId = "iotsimulatordeviceE20181"; //unique identifier you can supply indpendent from the device id in connection string/iot hub

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {

                Console.WriteLine("Argument index 0 not found: the Device IoT Hub connection string. Press any key to exit.");
                Console.ReadLine();
                return;
            }
            else
            {
                iotDeviceConnectionString = args[0].ToString();
            }


            Console.WriteLine("Simulated device\n");

            try
            {
                InitDeviceClient().Wait();

                cToken = new CancellationTokenSource();

                //report back into Reported properties twin
                SendDeviceReportedDie();

                //Start infinite telemetry loop for simulator
                SendDeviceToCloudMessagesAsync(cToken.Token);

                Console.ReadLine();
                cToken.Cancel();
            }
            catch (Exception exc)
            {
                Console.WriteLine($"Error in simulator: {exc.Message}");
            }

        }

        //Initiate DeviceClient, connect and subscribe to device twin updates
        static public async Task InitDeviceClient()
        {
            try
            {
                MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                // DEV only! bypass certs validation
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                ITransportSettings[] settings = { mqttSetting };

                deviceClient = DeviceClient.CreateFromConnectionString(iotDeviceConnectionString, settings);
                await deviceClient.OpenAsync();

                Console.WriteLine($"Connected to IoT Hub with connection string [{iotDeviceConnectionString}]");

                //read twin  setting upon first load
                var twin = await deviceClient.GetTwinAsync();
                await onDesiredPropertiesUpdate(twin.Properties.Desired, deviceClient);

                //register for Twin desiredProperties changes
                await deviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);

                //callback for generic direct method calls
                //todo change the callback to actual method name and finalize callback implementation
                deviceClient.SetMethodHandlerAsync("Off", HandleDirectMethod, null).Wait();

            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error in simulator: {ex.Message}");
            }

        }


        public static async void SendDeviceReportedDie()
        {
            try
            {
                Console.WriteLine("Sending device reported twin properties:");
                Random random = new Random();

                reportedProperties["dieNumber"] = random.Next(1, 6);
                Console.WriteLine(JsonConvert.SerializeObject(reportedProperties));
                
                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
            }
        }

        private static async Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                string setting = "Interval";
                if (desiredProperties.Contains(setting))
                {
                   Interval = desiredProperties["Interval"];
                   AcknowledgeSettingChange(desiredProperties, setting);
                }
                setting = "fanSpeed";
                if (desiredProperties.Contains(setting))
                {
                    // Act on setting change, then
                    AcknowledgeSettingChange(desiredProperties, setting);
                }
                setting = "setVoltage";
                if (desiredProperties.Contains(setting))
                {
                    // Act on setting change, then
                    AcknowledgeSettingChange(desiredProperties, setting);
                }
                setting = "setCurrent";
                if (desiredProperties.Contains(setting))
                {
                    // Act on setting change, then
                    AcknowledgeSettingChange(desiredProperties, setting);
                }
                setting = "activateIR";
                if (desiredProperties.Contains(setting))
                {
                    // Act on setting change, then
                    AcknowledgeSettingChange(desiredProperties, setting);
                }

                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }

        }


        private static async void SendDeviceToCloudMessagesAsync(CancellationToken ct)
        {
            int messageId = 1;
            Random rand = new Random();

            while (true)
            {
                double currentTemperature = minTemperature + rand.NextDouble() * 15;
                double currentHumidity = minHumidity + rand.NextDouble() * 20;
                double currentPressure = minPressure + rand.NextDouble() * 100;

                MessageBody messageBody = new MessageBody();
                messageBody.SimulatorDeviceId = simulatedDeviceId;
                messageBody.Messageid = messageId;
                messageBody.Humitidy = currentHumidity;
                messageBody.Temperature = currentTemperature;
                messageBody.Pressure = currentPressure;
                messageBody.FanMode = (currentTemperature > 25) ? "1" : "0";
                if (currentTemperature > 30)
                    messageBody.Overheated = "ERRORVERHEAT";

                var messageString = JsonConvert.SerializeObject(
                    messageBody,
                    Newtonsoft.Json.Formatting.None,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                ct.ThrowIfCancellationRequested();

                await deviceClient.SendEventAsync(message);
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

                await Task.Delay(Interval);
            }
        }

        static Task<MethodResponse> HandleDirectMethod(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Direct Method ({methodRequest.Name}) invoked...  ");
            Console.WriteLine("Returning response for method {0}", methodRequest.Name);

            string result = "'DM call sucess'";
            return Task.FromResult(new MethodResponse(System.Text.Encoding.UTF8.GetBytes(result), 200));
        }

        private static void AcknowledgeSettingChange(TwinCollection desiredProperties, string setting)
        {
            reportedProperties[setting] = new
            {
                value = desiredProperties[setting]["value"],
                status = "completed",
                desiredVersion = desiredProperties["$version"],
                message = "Processed"
            };
            Console.WriteLine($"Reported properties bag update for key ({setting}).");
        }


    }

    class MessageBody
    {
        public DateTime timecreated { get; set; } = DateTime.Now;
        [JsonProperty("messageid")]
        public int Messageid { get; set; }
        [JsonProperty("simulatordeviceid")]
        public string SimulatorDeviceId { get; set; }
        [JsonProperty("temp")]
        public double Temperature { get; set; }
        [JsonProperty("humidity")]
        public double Humitidy { get; set; }
        
        [JsonProperty("pressure")]
        public double Pressure { get; set; }

        [JsonProperty("fanmode")]
        public string FanMode { get; set; }

        [JsonProperty("overheat")]
        public string Overheated { get; set; }
    }


}
