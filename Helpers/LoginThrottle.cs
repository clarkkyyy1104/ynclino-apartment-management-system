using System.Collections.Concurrent;

namespace YnclinoApartmentManagementSystem.Helpers
{
    // A username with a guessable password is only dangerous if an attacker may
    // keep guessing. This caps the attempts. It is deliberately in-memory: the
    // system runs as a single instance on one machine, and a counter that resets
    // when the app restarts is still enough to stop an online guessing run, which
    // needs thousands of tries in a row to work.
    public static class LoginThrottle
    {
        private const int MaxAttempts = 5;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        private sealed class Entry
        {
            public int Failures;
            public DateTime LockedUntil;
        }

        private static readonly ConcurrentDictionary<string, Entry> Attempts = new();

        private static string Key(string username) => username.Trim().ToLowerInvariant();

        // How long the caller must wait, or null when they may try now.
        public static TimeSpan? RetryAfter(string username)
        {
            if (!Attempts.TryGetValue(Key(username), out var e)) return null;
            var left = e.LockedUntil - DateTime.UtcNow;
            return left > TimeSpan.Zero ? left : null;
        }

        public static void RecordFailure(string username)
        {
            var e = Attempts.GetOrAdd(Key(username), _ => new Entry());
            lock (e)
            {
                // a lock that has already expired starts the count again
                if (e.LockedUntil <= DateTime.UtcNow && e.Failures >= MaxAttempts) e.Failures = 0;

                e.Failures++;
                if (e.Failures >= MaxAttempts) e.LockedUntil = DateTime.UtcNow.Add(Window);
            }
        }

        public static void RecordSuccess(string username) => Attempts.TryRemove(Key(username), out _);
    }
}
