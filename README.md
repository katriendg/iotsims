# IoT Simulators stuff with Azure IoT Hub and IoT Central

## Dotnet core simulator for IoT Hub and IoT Central

1. The `/dnetsimulator` folder contains a .NET Core 2.0 console application that sends telemetry
1. To run, clone this repo and navigate to the above folder in your command line terminal or Visual Studio Code Terminal
1. Type `dotnet run "[yourdeviceconnectionstringhere]"` to start sending telemetry to a device in IoT Hub
1. For IoT Central, leverage the Sample DevKits sample, create a real device and use the device's connection string to run the simulator as previous step. Telemetry will now be sent to IoT Central

### Disclaimer
This code is not production ready and serves only to illustrate technology and for testing.