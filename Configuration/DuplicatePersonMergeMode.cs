using System.ComponentModel;

namespace Emby.GuestStarCleaner.Configuration
{
    /// <summary>
    /// Controls whether/when a duplicate Person entity (same name, different
    /// Emby person Id, found between a series' cast and an episode's guest
    /// stars) is automatically repaired by repointing media items from the
    /// "runt" Id onto a "winner" Id. Every tier below Off still requires at
    /// least one populated provider Id (Tmdb or Tvdb) to match - this never
    /// merges on name alone. Each tier is a strict superset of the one
    /// above it.
    /// </summary>
    public enum DuplicatePersonMergeMode
    {
        [Description("Off (Default) - never merge, only log")]
        Off = 0,

        [Description("Conservative - merge only when both Tmdb and Tvdb are populated on each side and match")]
        MergeWhenBothIdsPopulatedAndMatch = 1,

        [Description("Moderate - merge when at least one of Tmdb/Tvdb is populated on each side and matches")]
        MergeWhenOneIdPopulatedAndMatches = 2,

        [Description("Reckless - merge when one of Tmdb/Tvdb matches and the other is blank on one side (not disagreeing)")]
        MergeWhenOneIdPopulatedAndOtherBlank = 3,
    }
}
