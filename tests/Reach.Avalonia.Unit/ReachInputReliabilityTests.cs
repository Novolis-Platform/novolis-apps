using Avalonia.Input;
using Novolis.Reach.Client;
using Novolis.Avalonia.Reach;

namespace Reach.Avalonia.Unit;

public sealed class ReachInputReliabilityTests
{
    [Test]
    public async Task CommonWindowsKeysMapToExpectedVirtualKeys()
    {
        var expected = new Dictionary<Key, ushort>
        {
            [Key.RWin] = 0x5C,
            [Key.Apps] = 0x5D,
            [Key.CapsLock] = 0x14,
            [Key.NumLock] = 0x90,
            [Key.Scroll] = 0x91,
            [Key.PrintScreen] = 0x2C,
            [Key.Pause] = 0x13,
            [Key.F12] = 0x7B,
        };

        foreach (var pair in expected)
        {
            await Assert.That(ReachClientKeyMap.TryGetVirtualKey(
                pair.Key,
                out var virtualKey)).IsTrue();
            await Assert.That(virtualKey).IsEqualTo(pair.Value);
        }
    }

    [Test]
    public async Task PrintableOemKeysAreLeftToTextInput()
    {
        await Assert.That(ReachClientKeyMap.IsPrintable(Key.OemPlus)).IsTrue();
        await Assert.That(ReachClientKeyMap.IsPrintable(Key.OemOpenBrackets)).IsTrue();
        await Assert.That(ReachClientKeyMap.IsPrintable(Key.OemQuotes)).IsTrue();
        await Assert.That(ReachClientKeyMap.IsPrintable(Key.LeftCtrl)).IsFalse();
    }

    [Test]
    public async Task PressedKeyTrackerSuppressesDuplicatesAndReleasesAll()
    {
        var tracker = new ReachPressedKeyTracker();

        await Assert.That(tracker.TryPress(Key.LeftCtrl)).IsTrue();
        await Assert.That(tracker.TryPress(Key.LeftCtrl)).IsFalse();
        await Assert.That(tracker.TryPress(Key.LeftShift)).IsTrue();
        await Assert.That(tracker.TryRelease(Key.RightCtrl)).IsFalse();

        var released = tracker.ReleaseAll();

        await Assert.That(released).Contains(Key.LeftCtrl);
        await Assert.That(released).Contains(Key.LeftShift);
        await Assert.That(tracker.TryRelease(Key.LeftCtrl)).IsFalse();
    }

    [Test]
    public async Task WindowsAndTailscaleEndpointsChooseDifferentInitialProfiles()
    {
        await Assert.That(
                ReachClientViewFrames.ResolveInitialVideoProfileKind(
                    "tcp://192.168.1.20:19800"))
            .IsEqualTo(ReachVideoProfileKind.Lan);
        await Assert.That(
                ReachClientViewFrames.ResolveInitialVideoProfileKind(
                    "quic://reach.tail123.ts.net:19800"))
            .IsEqualTo(ReachVideoProfileKind.Routed);
    }
}
