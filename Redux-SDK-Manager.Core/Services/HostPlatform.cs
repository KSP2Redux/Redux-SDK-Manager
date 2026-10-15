using System.Runtime.InteropServices;

namespace Redux_SDK_Manager.Services;

/// <summary>The host OS family.</summary>
public enum HostOs
{
    Windows,
    Linux,
    MacOS
}

/// <summary>
/// The host OS and runtime identifier, injected so OS-specific paths (Unity Hub, Steam, release assets)
/// can be tested on any OS.
/// </summary>
public interface IHostPlatform
{
    HostOs Os { get; }

    /// <summary>The .NET runtime identifier of the running build, e.g. win-x64, linux-x64, osx-arm64.</summary>
    string RuntimeIdentifier { get; }
}

public sealed class HostPlatform : IHostPlatform
{
    public HostOs Os { get; } =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? HostOs.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? HostOs.MacOS
        : HostOs.Linux;

    public string RuntimeIdentifier => RuntimeInformation.RuntimeIdentifier;
}
