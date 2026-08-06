using System;

namespace Redux_SDK_Manager.Services;

/// <summary>The running application version. Anchored to the Core assembly so the GUI and CLI, which
/// are stamped with the same shared version, always report the same number.</summary>
public interface IAppVersion
{
    Version? Current { get; }
}

public sealed class AppVersion : IAppVersion
{
    public Version? Current => typeof(AppVersion).Assembly.GetName().Version;
}
