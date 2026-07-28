using Novolis.Audio.Live;

namespace Novolis.Audio.Live.Host.Render;

/// <summary>Binds a <see cref="LiveSession"/> and produces audible audio.</summary>
/// <remarks>
/// Host-local v0 engine until <c>Novolis.Audio.Live.Render</c> is published to GitHub Packages.
/// </remarks>
internal interface ILiveAudioEngine : IAsyncDisposable
{
    void Bind(LiveSession session);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
