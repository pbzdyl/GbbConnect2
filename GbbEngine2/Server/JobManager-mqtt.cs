using System.Text.Json;
using GbbConnect2Protocol;
using GbbEngine2.Configuration;
using GbbEngine2.Drivers;
using GbbEngine2.Drivers.Random;
using GbbEngine2.Drivers.SolarmanV5;
using GbbLibSmall;
using MQTTnet;

namespace GbbEngine2.Server
{
    public partial class JobManager
    {
        private static MqttClientFactory mqttFactory = new();

        private async void OurMqttService(Configuration.Parameters Parameters, CancellationToken ct, IOurLog log)
        {

            try
            {
#if DEBUG
                // wait for server to start
                await Task.Delay(5 * 1000, ct);
#endif

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        // start
                        //log.OurLog(LogLevel.Information, "MqttService: starting");
                        await OurMqttService_DoWork(Parameters, ct, log);

                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception ex) // eg System.Threading.Tasks.TaskCanceledException
                    {
                        // log
                        log.OurLog(LogLevel.Error, $"MqttServices: {ex}");
                    }

                    // try again after delay
                    if (!ct.IsCancellationRequested)
                    {
#if DEBUG
                        log.OurLog(LogLevel.Error, $"MqttServices: waiting 10 sec");
                        await Task.Delay(10 * 1000, ct);
#else
                        log.OurLog(LogLevel.Error, $"MqttServices: waiting 5min");
                        await Task.Delay(5*60 * 1000, ct);
#endif
                    }
                }
            }
            catch(TaskCanceledException)
            {
            }
            // log
            log.OurLog(LogLevel.Information, "MqttService: finished");
        }


        private async Task ConnectToMqtt(Parameters Parameters, Plant plant, IMqttClient client, CancellationToken ct, IOurLog log)
        {
            if (string.IsNullOrWhiteSpace(plant.GbbOptimizer_PlantId))
                throw new ApplicationException("GbbConnect2: No PlantId!");
            if (string.IsNullOrWhiteSpace(plant.GbbOptimizer_Mqtt_Address))
                throw new ApplicationException("GbbConnect2: No Mqtt address!");

            var b = new MqttClientOptionsBuilder()
                .WithClientId($"GbbConnect2_{plant.GbbOptimizer_PlantId?.ToString()}")
                .WithCleanSession(true)
                .WithTlsOptions(new MqttClientTlsOptions()
                {
                    UseTls = true,
                    // 2023-12-15: nie ma juz potrzeby
                    //IgnoreCertificateChainErrors = true,
                })
                .WithTcpServer(plant.GbbOptimizer_Mqtt_Address, plant.GbbOptimizer_Mqtt_Port)
                .WithCredentials(plant.GbbOptimizer_PlantId?.ToString(), plant.GbbOptimizer_PlantToken);
            //.WithSessionExpiryInterval(2*60); // nie wiadomo w jakich to jest jednostkach!

            // connect
            await client.ConnectAsync(b.Build(), ct);


            // subsribe
            await client.SubscribeAsync(
                mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(q =>
                        q.WithTopic($"{plant.GbbOptimizer_PlantId?.ToString()}/ModbusInMqtt/toDevice")
                        .WithAtLeastOnceQoS()
                        )
                .Build()
                , ct);

        }

        private async Task OurMqttService_DoWork(Configuration.Parameters Parameters, CancellationToken ct, IOurLog log)
        {

            int Counter = 0;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    DateTime LoopStartTime = DateTime.Now;

                    // =====================================
                    // Connect / Reconnect
                    // =====================================
                    foreach (var plant in Parameters.Plants)
                    {
                        try
                        {
                            if (plant.PlantState!=null && plant.PlantState.MqttClient==null
                                && plant.IsDisabled == 0 && plant.GbbOptimizer_PlantId != null && plant.GbbOptimizer_PlantToken != null)
                            {
                                log.OurLog(LogLevel.Information, $"{plant.Name}: Starting Mqtt");
                                var client = mqttFactory.CreateMqttClient();

                                // callback
                                client.ApplicationMessageReceivedAsync += e => { return MqttClient_MessageReceivedAsync(Parameters, e, plant, log); };

                                // Use builder classes where possible in this project.
                                await ConnectToMqtt(Parameters, plant, client, ct, log);

                                // save client
                                plant.PlantState!.MqttClient = client;

                                log.OurLog(LogLevel.Information, $"{plant.Name}: Started Mqtt");
                                Counter++;
                            }


                            // reconnect
                            if (plant.PlantState != null && plant.PlantState.MqttClient != null
                                && !plant.PlantState.MqttClient.IsConnected)
                            {
                                log.OurLog(LogLevel.Information, $"{plant.Name}: Mqtt: Reconnect");
                                await ConnectToMqtt(Parameters, plant, plant.PlantState.MqttClient, ct, log);
                                log.OurLog(LogLevel.Information, $"{plant.Name}: Mqtt: Reconnected");
                            }
                        }
                        catch (TaskCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            log.OurLog(LogLevel.Error, $"{plant.Name}: {ex.Message}");
                        }

                        ct.ThrowIfCancellationRequested();
                    }
                    // nothing to do
                    if (Counter == 0)
                        break;


                    // ===============
                    // keep alive
                    // ===============
                    foreach (var plant in Parameters.Plants)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            if (plant.PlantState!.MqttClient!= null && plant.PlantState!.MqttClient.IsConnected)
                            {

                                if (Parameters.IsVerboseLog)
                                {
                                    log.OurLog(LogLevel.Information, $"{plant.Name}: Mqtt: Sending keepalive");
                                }


                                await plant.PlantState!.MqttClient.PublishAsync(
                                    new MqttApplicationMessageBuilder()
                                    .WithTopic($"{plant.GbbOptimizer_PlantId?.ToString()}/keepalive")
                                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                                    .Build()
                                    , ct);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                        }
                        catch (Exception ex) 
                        {
                            log.OurLog(LogLevel.Error, $"{plant.Name}: Mqtt: {ex.Message}");
                        }
                    }

                    // =====================================
                    // try keep 1min between keep-alive
                    // =====================================
                    var ms = (int)(LoopStartTime.AddMinutes(1) - DateTime.Now).TotalMilliseconds;
                    if (ms > 0)
                        await Task.Delay(ms, ct);
                }
            }
            finally
            {
                foreach(var plant in Parameters.Plants)
                {
                    if (plant.PlantState!.MqttClient != null)
                    {
                        try
                        {
                            // disconnect
                            log.OurLog(LogLevel.Information, $"{plant.Name}: Disconnecting Mqtt");
                            var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();
                            await plant.PlantState!.MqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            log.OurLog(LogLevel.Error, $"{plant.Name}: {ex.Message}");
                        }
                    }
                }

            }

        }


        // ======================================


        // ======================================


        private async Task MqttClient_MessageReceivedAsync(Configuration.Parameters Parameters, MqttApplicationMessageReceivedEventArgs arg, Configuration.Plant Plant, IOurLog log)
        {
            string? Operation = null;

            // options for json serialization
            var SerOpt = new JsonSerializerOptions();
            SerOpt.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;


            try
            {
                var seg = arg.ApplicationMessage.ConvertPayloadToString();
                if (Parameters.IsVerboseLog)
                {
                    log.OurLog(LogLevel.Information, $"{Plant.Name}: Mqtt: Received request: {seg}");
                }


                var jsonOptions = new JsonSerializerOptions();
                jsonOptions.AllowTrailingCommas = true;
                jsonOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

                var Header = JsonSerializer.Deserialize<Header>(seg, jsonOptions);
                if (Header != null)
                {
                    Header.GbbVersion = Parameters.APP_VERSION;
                    Header.GbbEnvironment = Parameters.APP_ENVIRONMENT; 

                    IDriver? drv = null;
                    try
                    {
                        // get driver
                        switch (Plant.DriverNo)
                        {
                            case (int)GbbEngine2.Drivers.DriverInfo.Drivers.i000_SolarmanV5:
                                SolarmanV5Driver sm = new SolarmanV5Driver(Parameters, Plant.AddressIP, Plant.PortNo, Plant.SerialNumber, log);
                                sm.Connect();
                                drv = sm;
                                break;

                            case (int)GbbEngine2.Drivers.DriverInfo.Drivers.i999_Random:
                                drv = new RandomDriver();
                                break;

                            default:
                                throw new ApplicationException("Unknown driver no: " + Plant.DriverNo);
                        }

                        if (Header.Lines != null)
                        {
                            for (int i = 0; i < Header.Lines.Length; i++)
                            {
                                var line = Header.Lines[i];
                                if (line != null)
                                {
                                    try
                                    {
                                        if (line.Modbus != null)
                                        {
                                            // send to device
                                            line.Modbus = GbbLibSmall2.Convert.ButesToString(await drv.SendDataToDevice(GbbLibSmall2.Convert.StringToBytes(line.Modbus)));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        line.Error = ex.Message;
                                        // clear rest  Modbus data
                                        for (; i < Header.Lines.Length; i++)
                                            Header.Lines[i].Modbus = null;
                                        break;
                                    }
                                }

                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Header.Error = ex.Message;
                        if (Header.Lines!=null)
                            for (int i = 0; i < Header.Lines.Length; i++)
                                Header.Lines[i].Modbus = null;
                    }
                    finally
                    {
                        if (drv != null)
                        {
                            drv.Dispose();
                            drv = null;
                        }
                    }



                    var payload = JsonSerializer.Serialize(Header, jsonOptions);
                    if (Parameters.IsVerboseLog)
                    {
                        log.OurLog(LogLevel.Information, $"{Plant.Name}: Mqtt: Send response: {payload}");
                    }

                    // send response
                    await Plant.PlantState!.MqttClient!.PublishAsync(
                        new MqttApplicationMessageBuilder()
                       .WithTopic($"{Plant.GbbOptimizer_PlantId?.ToString()}/ModbusInMqtt/fromDevice")
                       .WithPayload(payload)
                       .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                       .Build()
                       , CancellationToken.None);

                }

            }
            catch (Exception ex)
            {
                log.OurLog(LogLevel.Error, $"{Plant.Name}: {ex.Message}");

                if (Operation != null && Plant.PlantState!.MqttClient != null)
                {
                    try
                    {
                        // send error resonse
                        //var Res = new Response();
                        //Res.Operation = Operation;
                        //Res.Status = "ERROR";
                        //Res.ErrDesc = ex.Message;

                        //await Plant.PlantState!.MqttClient.PublishAsync(
                        //    new MqttApplicationMessageBuilder()
                        //   .WithTopic($"{Plant.GbbOptimizer_PlantId.ToString()}/dataresponse")
                        //   .WithPayload(JsonSerializer.Serialize(Res, SerOpt))
                        //   .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                        //   .Build()
                        //   , CancellationToken.None);

                    }
                    catch (Exception ex2)
                    {
                        log.OurLog(LogLevel.Error, $"{Plant.Name}: {ex2.Message}");
                    }

                }
                
            }


        }

    }
}
