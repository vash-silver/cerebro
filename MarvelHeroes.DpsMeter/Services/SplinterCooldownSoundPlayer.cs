using System;
using System.IO;
using System.Windows.Media;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Plays the cooldown-expired audio cue.  Tries the user's configured custom sound file
/// first (any format WPF's <see cref="MediaPlayer"/> can decode -- WAV / MP3 / WMA / AAC);
/// falls back to <c>System.Media.SystemSounds.Asterisk</c> when no custom file is set, the
/// path doesn't exist, or playback throws.
///
/// <para>Threading: must be called on the UI dispatcher thread.  WPF's <see cref="MediaPlayer"/>
/// is single-threaded per instance and the presenter's CooldownExpired event already lands
/// on the UI thread via the 4 Hz decay timer, so callers don't need to marshal explicitly.</para>
///
/// <para>A single static <see cref="MediaPlayer"/> instance is reused across plays.  The
/// cooldown is 7 minutes so we never overlap; <see cref="MediaPlayer.Open"/> stops any
/// previous playback before starting the new one.</para>
/// </summary>
public static class SplinterCooldownSoundPlayer
{
    private static MediaPlayer? s_mediaPlayer;

    /// <summary>Play either the configured file (if non-empty and on disk) or the system
    /// asterisk sound.  Never throws -- audio failures are best-effort.</summary>
    /// <param name="customSoundPath">Absolute path to a sound file, or null/empty to use
    /// the system fallback.</param>
    /// <param name="volume">Playback volume in [0.0, 1.0].  Applied to the custom-file path
    /// only; the system asterisk fallback has no programmatic volume and follows the
    /// user's Windows notification-sound level instead.  Clamped to the legal range.</param>
    /// <returns><c>true</c> if a custom file was played, <c>false</c> if the fallback was
    /// used (file missing, playback threw, or no path configured).</returns>
    public static bool Play(string? customSoundPath, double volume = 1.0)
    {
        if (!string.IsNullOrWhiteSpace(customSoundPath))
        {
            try
            {
                if (File.Exists(customSoundPath))
                {
                    // Reuse the same MediaPlayer across plays -- creating a fresh one each
                    // time is fine too, but the instance can outlive the call and the GC
                    // hasn't always been kind to "fire and forget" MediaPlayer objects in
                    // the past.  Keeping a single rooted instance sidesteps that entirely.
                    s_mediaPlayer ??= new MediaPlayer();
                    // Setting Volume BEFORE Open() ensures the very first frame plays at the
                    // intended level (otherwise WPF briefly plays the previous volume).
                    s_mediaPlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
                    s_mediaPlayer.Open(new Uri(customSoundPath, UriKind.Absolute));
                    s_mediaPlayer.Play();
                    return true;
                }
            }
            catch
            {
                // Swallow and fall through to the system sound -- a corrupt file or a
                // missing codec shouldn't take down the cooldown notification entirely.
            }
        }

        // The system asterisk has no Volume hook -- it uses Windows' notification-sound
        // level which the user controls via Sound Mixer.  Document this in the UI so the
        // volume slider's no-effect-when-fallback-is-active behaviour isn't confusing.
        try { System.Media.SystemSounds.Asterisk.Play(); }
        catch { /* really nothing to do here; sound is best-effort */ }
        return false;
    }
}
