using System;
using BepInEx.Logging;

namespace IntegrationCore
{
    public interface IPluginCore
    {
        bool Active { get; }
        ActionManager ActionManager { get; }
        ManualLogSource Log { get; }
        double RandomRange(int actionDelayMin, int actionDelayMax);
        void AddActions(Action<Type> addAction);
    }
}