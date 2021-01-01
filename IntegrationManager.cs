using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;

namespace IntegrationCore
{
    public class IntegrationManager
    {
        private readonly IPluginCore _plugin;
        private readonly string _connection;
        private ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();
        private CancellationTokenSource _source;
        private Task _task;
        private readonly ManualLogSource _logger;

        public IntegrationManager(IPluginCore plugin, string connection)
        {
            _plugin = plugin;
            _connection = connection;
            _logger = plugin.Log;
        }

        public void Start()
        {
            _messages = new ConcurrentQueue<string>();
            _source = new CancellationTokenSource();
            var token = _source.Token;
            _task = Task.Factory.StartNew(() =>
            {
                _logger.LogInfo("Starting Integration connection");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using (var client = new NamedPipeClientStream(".", _connection, PipeDirection.In))
                        {
                            using (var reader = new StreamReader(client))
                            {
                                
                                while (!token.IsCancellationRequested && !client.IsConnected)
                                {
                                    try
                                    {
                                        client.Connect(1000);
                                    }
                                    catch (TimeoutException)
                                    {
                                        // Ignore
                                    }
                                    catch (Win32Exception)
                                    {
                                        Thread.Sleep(500);
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogError($"Error in pipe connection: {e}");
                                        Thread.Sleep(500);
                                    }
                                }
                                _logger.LogDebug("Connected to Integration");
                                while (!token.IsCancellationRequested && client.IsConnected)
                                {
                                    var readTask = reader.ReadLineAsync();
                                    if (readTask.Wait(5000, token))
                                    {
                                        var line = readTask.Result;
                                        if (line != null)
                                        {
                                            if (_plugin.Active)
                                            {
                                                Handle(line);
                                            }
                                            else
                                            {
                                                _messages.Enqueue(line);
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogDebug("Empty data, reconnect");
                                            break;
                                        }
                                        reader.DiscardBufferedData();
                                    }
                                    else
                                    {
                                        _logger.LogDebug("Read timeout, reconnect");
                                        break;
                                    }

                                    Thread.Sleep(50);
                                }
                                _logger.LogDebug("Disconnected to Integration");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"Error in socket connection: {e}");
                    }
                }
            }, token);
        }

        public void Close()
        {
            if (_source == null) return;
            _source.Cancel(true);
            _task?.Wait(1000);
            _task = null;
            _messages = null;
        }

        public void Reconnect()
        {
            Close();
            Start();
        }
        
        public void Update()
        {
            while (_messages.TryDequeue(out var line))
            {
                Handle(line);
            }
        }

        private void Handle(string line)
        {
            if (line.StartsWith("Action: "))
            {
                _logger.LogDebug(line);
                var action = line.Substring(8);
                _plugin.ActionManager.HandleAction(action);
            }
            else if (line.StartsWith("Message: "))
            {
                _logger.LogDebug(line);
                var message = line.Substring(9);
                _plugin.ActionManager.HandleMessage(message);
            }
        }
    }
}