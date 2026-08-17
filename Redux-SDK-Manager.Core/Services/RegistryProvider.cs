using Microsoft.Win32;

namespace Redux_SDK_Manager.Services;

public interface IRegistryProvider
{
    public string GetValue(string key, string value, string def="");
}

public class WindowsRegistryProvider : IRegistryProvider
{
    public string GetValue(string key, string value, string def="")
    {
#pragma warning disable CA1416
        return (string)(Registry.GetValue(key, value, def) ?? def);
#pragma warning restore CA1416
    }
}