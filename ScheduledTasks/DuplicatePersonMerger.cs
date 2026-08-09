using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace Emby.GuestStarCleaner.ScheduledTasks
{
    /// <summary>
    /// Detects duplicate Person library entities (same name, different Emby
    /// person Id) surfaced when comparing a series' cast against an
    /// episode's guest stars, and - when the configured
    /// DuplicatePersonMergeMode allows it - repairs them by repointing every
    /// media item that references the "runt" Id onto a "winner" Id, then
    /// attempting to delete the now-orphaned runt Person item.
    ///
    /// All output from this class is logged under the
    /// [DuplicatePersonDetection] subheading so it can be filtered
    /// independently of the plugin's main duplicate-guest-star logging.
    /// </summary>
    internal static class DuplicatePersonMerger
    {
        private const string LogTag = "[DuplicatePersonDetection]";

        /// <summary>
        /// Examines one series/episode person-name match that failed to
        /// match by Id, logs the concern (always), and performs a repair
        /// merge if the configured mode's safety conditions are met and the
        /// per-run merge cap has not been reached.
        /// </summary>
        /// <returns>True if a merge was performed this call.</returns>
        public static bool EvaluateAndRepair(
            ILogger log,
            ILibraryManager libraryManager,
            Configuration.PluginConfiguration config,
            BaseItem series,
            BaseItem episode,
            PersonInfo seriesPerson,
            PersonInfo episodePerson,
            bool mergeAlreadyPerformedThisRun)
        {
            string seriesTmdb = seriesPerson.GetProviderId(MetadataProviders.Tmdb);
            string seriesTvdb = seriesPerson.GetProviderId(MetadataProviders.Tvdb);
            string episodeTmdb = episodePerson.GetProviderId(MetadataProviders.Tmdb);
            string episodeTvdb = episodePerson.GetProviderId(MetadataProviders.Tvdb);

            log.Debug(
                "Guest Star Cleaner: {0} Name match but Id mismatch for '{1}' - series person Id={2} (Tmdb={3}, Tvdb={4}) vs episode person Id={5} (Tmdb={6}, Tvdb={7}) in {8}",
                LogTag,
                seriesPerson.Name,
                seriesPerson.Id,
                NullToBlank(seriesTmdb),
                NullToBlank(seriesTvdb),
                episodePerson.Id,
                NullToBlank(episodeTmdb),
                NullToBlank(episodeTvdb),
                DescribeItem(episode));

            var mode = config.DuplicatePersonMergeMode;
            if (mode == Configuration.DuplicatePersonMergeMode.Off)
            {
                return false;
            }

            if (config.EnableGSTestmode && mergeAlreadyPerformedThisRun)
            {
                log.Debug(
                    "Guest Star Cleaner: {0} Testmode On: skipping further merges this run (max 1 merge round per task run while testing) - would also have evaluated '{1}'",
                    LogTag,
                    seriesPerson.Name);
                return false;
            }

            if (!IsSafeToMerge(mode, seriesTmdb, seriesTvdb, episodeTmdb, episodeTvdb))
            {
                log.Debug(
                    "Guest Star Cleaner: {0} '{1}' does not meet the safety conditions for mode '{2}' - left as log-only",
                    LogTag,
                    seriesPerson.Name,
                    mode);
                return false;
            }

            PersonInfo winner = SelectWinner(seriesPerson, episodePerson);
            PersonInfo runt = ReferenceEquals(winner, seriesPerson) ? episodePerson : seriesPerson;

            if (config.EnableGSTestmode)
            {
                log.Info(
                    "Guest Star Cleaner: {0} Testmode On: would merge '{1}' - winner Id={2}, runt Id={3} - not performed",
                    LogTag,
                    seriesPerson.Name,
                    winner.Id,
                    runt.Id);
                return true;
            }

            PerformMerge(log, libraryManager, winner, runt);
            return true;
        }

        private static bool IsSafeToMerge(
            Configuration.DuplicatePersonMergeMode mode,
            string seriesTmdb,
            string seriesTvdb,
            string episodeTmdb,
            string episodeTvdb)
        {
            bool tmdbBothPopulated = !string.IsNullOrEmpty(seriesTmdb) && !string.IsNullOrEmpty(episodeTmdb);
            bool tvdbBothPopulated = !string.IsNullOrEmpty(seriesTvdb) && !string.IsNullOrEmpty(episodeTvdb);
            bool tmdbMatchesIfBothPopulated = !tmdbBothPopulated || string.Equals(seriesTmdb, episodeTmdb, StringComparison.OrdinalIgnoreCase);
            bool tvdbMatchesIfBothPopulated = !tvdbBothPopulated || string.Equals(seriesTvdb, episodeTvdb, StringComparison.OrdinalIgnoreCase);

            // Any populated-on-both-sides id that disagrees is an immediate
            // hard stop, regardless of mode - this is a real conflict, not
            // a data-completeness gap.
            if (!tmdbMatchesIfBothPopulated || !tvdbMatchesIfBothPopulated)
            {
                return false;
            }

            switch (mode)
            {
                case Configuration.DuplicatePersonMergeMode.MergeWhenBothIdsPopulatedAndMatch:
                    return tmdbBothPopulated && tvdbBothPopulated;

                case Configuration.DuplicatePersonMergeMode.MergeWhenOneIdPopulatedAndMatches:
                    return tmdbBothPopulated || tvdbBothPopulated;

                case Configuration.DuplicatePersonMergeMode.MergeWhenOneIdPopulatedAndOtherBlank:
                    // At least one populated id on at least one side that
                    // isn't contradicted by the other side (already
                    // enforced above), and at least one id present at all.
                    bool anyIdPresent =
                        !string.IsNullOrEmpty(seriesTmdb) || !string.IsNullOrEmpty(episodeTmdb) ||
                        !string.IsNullOrEmpty(seriesTvdb) || !string.IsNullOrEmpty(episodeTvdb);
                    return anyIdPresent;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Winner selection order (per project decision): (1) whichever
        /// PersonInfo has more populated provider ids (Tmdb + Tvdb count);
        /// (2) if tied, whichever has the lower/older Id; (3) if still
        /// tied, the series-level person wins as a final tiebreak.
        /// </summary>
        private static PersonInfo SelectWinner(PersonInfo seriesPerson, PersonInfo episodePerson)
        {
            int seriesPopulatedCount = CountPopulatedIds(seriesPerson);
            int episodePopulatedCount = CountPopulatedIds(episodePerson);

            if (seriesPopulatedCount != episodePopulatedCount)
            {
                return seriesPopulatedCount > episodePopulatedCount ? seriesPerson : episodePerson;
            }

            if (seriesPerson.Id != episodePerson.Id)
            {
                return seriesPerson.Id < episodePerson.Id ? seriesPerson : episodePerson;
            }

            return seriesPerson;
        }

        private static int CountPopulatedIds(PersonInfo person)
        {
            int count = 0;
            if (!string.IsNullOrEmpty(person.GetProviderId(MetadataProviders.Tmdb)))
            {
                count++;
            }

            if (!string.IsNullOrEmpty(person.GetProviderId(MetadataProviders.Tvdb)))
            {
                count++;
            }

            return count;
        }

        private static void PerformMerge(ILogger log, ILibraryManager libraryManager, PersonInfo winner, PersonInfo runt)
        {
            List<BaseItem> slavedItems;
            try
            {
                slavedItems = libraryManager.GetItemList(new InternalItemsQuery
                {
                    PersonIds = new[] { runt.Id },
                    Recursive = true,
                }).ToList();
            }
            catch (Exception ex)
            {
                log.ErrorException($"{LogTag} Error retrieving media items linked to runt person Id={runt.Id} - merge aborted", ex);
                return;
            }

            var novatedDescriptions = new List<string>();

            foreach (var item in slavedItems)
            {
                try
                {
                    var itemPeople = libraryManager.GetItemPeople(new InternalPeopleQuery
                    {
                        ItemIds = new[] { item.InternalId },
                        EnableIds = true,
                        EnableProviderIds = true,
                    });

                    bool changed = false;
                    foreach (var person in itemPeople)
                    {
                        if (person.Id == runt.Id)
                        {
                            person.Id = winner.Id;
                            person.Guid = winner.Guid;
                            person.ProviderIds = winner.ProviderIds;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        libraryManager.UpdatePeople(item, itemPeople);
                        novatedDescriptions.Add(DescribeItem(item));
                    }
                }
                catch (Exception ex)
                {
                    log.ErrorException($"{LogTag} Error repointing media item '{DescribeItem(item)}' from runt Id={runt.Id} to winner Id={winner.Id}", ex);
                }
            }

            log.Info(
                "Guest Star Cleaner: {0} Merged '{1}' - preserved Id={2}, deleted/orphaned Id={3}. Media items novated ({4}): {5}",
                LogTag,
                winner.Name,
                winner.Id,
                runt.Id,
                novatedDescriptions.Count,
                novatedDescriptions.Count > 0 ? string.Join("; ", novatedDescriptions) : "none");

            TryDeleteRuntPerson(log, libraryManager, runt);
        }

        private static void TryDeleteRuntPerson(ILogger log, ILibraryManager libraryManager, PersonInfo runt)
        {
            try
            {
                var runtItem = libraryManager.GetItemById(runt.Id) as Person;
                if (runtItem == null)
                {
                    log.Info(
                        "Guest Star Cleaner: {0} Runt person Id={1} could not be loaded as a Person item - left in place, may be cleaned up by a future library scan",
                        LogTag,
                        runt.Id);
                    return;
                }

                libraryManager.DeleteItem(
                    runtItem,
                    new DeleteOptions { DeleteFileLocation = false, DeleteFromExternalProvider = false },
                    notifyParentItem: false);

                log.Info(
                    "Guest Star Cleaner: {0} Deleted orphaned runt person Id={1} ('{2}')",
                    LogTag,
                    runt.Id,
                    runt.Name);
            }
            catch (Exception ex)
            {
                log.ErrorException(
                    $"{LogTag} Could not delete orphaned runt person Id={runt.Id} - left in place, may be cleaned up by a future library scan",
                    ex);
            }
        }

        /// <summary>
        /// Produces a clear, type-prefixed display string for a media item
        /// in merge logs: movies show their year, episodes show their
        /// season/episode index plus episode and series name.
        /// </summary>
        private static string DescribeItem(BaseItem item)
        {
            if (item is Episode episode)
            {
                string seasonEpisode =
                    (episode.ParentIndexNumber?.ToString("D2") ?? "??") + "E" +
                    (episode.IndexNumber?.ToString("D2") ?? "??");
                return $"Episode - S{seasonEpisode} - {episode.Name} ({episode.SeriesName})";
            }

            if (item is Movie movie)
            {
                string year = movie.ProductionYear.HasValue ? movie.ProductionYear.Value.ToString() : "year unknown";
                return $"Movie - {movie.Name} ({year})";
            }

            return $"{item.GetType().Name} - {item.Name}";
        }

        private static string NullToBlank(string value)
        {
            return string.IsNullOrEmpty(value) ? "(blank)" : value;
        }
    }
}
