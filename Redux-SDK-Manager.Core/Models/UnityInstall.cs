namespace Redux_SDK_Manager.Models;

/// <summary>An installed Unity editor discovered on disk (via Unity Hub's known locations).</summary>
public sealed record UnityInstall(string Version, string ExecutablePath);
