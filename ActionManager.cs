using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BepInEx.Logging;
using Humanizer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IntegrationCore
{
    public class ActionManager
    {
        private Dictionary<string, Type> _actions = new Dictionary<string, Type>();
        private ConcurrentQueue<BaseAction> _actionQueue = new ConcurrentQueue<BaseAction>();
        private readonly IPluginCore _plugin;
        private readonly ManualLogSource _logger;
        
        public ActionManager(IPluginCore plugin)
        {
            _plugin = plugin;
            _logger = plugin.Log;
            plugin.AddActions(AddAction);
        }

        ~ActionManager()
        {
            _actions.Clear();
            _actions = null;
            _actionQueue = null;
        }

        private void AddAction(Type action)
        {
            if (!typeof(BaseAction).IsAssignableFrom(action))
            {
                _logger.LogWarning($"Action {action} was of wrong type");
                return;
            }

            var type = action.Name.Underscore();
            _actions.Add(type, action);
            _logger.LogDebug($"Added action: {type}");
        }

        public void HandleAction(string rawAction)
        {
            _logger.LogDebug($"ActionManager: Handling action {rawAction}");
            try
            {
                var o = JsonConvert.DeserializeObject<JObject>(rawAction);
                var type = (string) o["type"];

                if (type == null || !_actions.ContainsKey(type))
                {
                    _logger.LogError($"Error finding type of action, type was {type}");
                    return;
                }
                var actionType = _actions[type];
                
                var actionObj = o.ToObject(actionType);
                if (!(actionObj is BaseAction action)) return;
                
                if (action.DelayMin > 0 && action.DelayMax >= action.DelayMin)
                {
                    action.TryAfter = DateTime.Now + TimeSpan.FromMilliseconds(_plugin.RandomRange(action.DelayMin, action.DelayMax));
                }
                _actionQueue.Enqueue(action);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error parsing action: {e}");
            }
        }

        public void HandleMessage(string message)
        {
            _logger.LogDebug($"ActionManager: Handling message {message}");
            _actionQueue.Enqueue(new MessageAction(message));
        }

        public void Update()
        {
            if (!_actionQueue.TryDequeue(out var action)) return;
            
            if (action.TryAfter.HasValue)
            {
                if (action.TryAfter.Value > DateTime.Now)
                {
                    _actionQueue.Enqueue(action);
                    return;
                }

                action.TryAfter = null;
            }
                
            var response = action.Handle();
            switch (response)
            {
                case ActionResponse.Retry:
                    _actionQueue.Enqueue(action);
                    break;
                case ActionResponse.Done:
                    break;
            }
        }
    }

    public enum ActionResponse
    {
        Done,
        Retry
    }
}