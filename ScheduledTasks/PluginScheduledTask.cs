using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.GuestStarCleaner.ScheduledTasks
{
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        /// <summary>
        /// Stable identifier for this task, used both by Emby's task manager
        /// and by the config page (ConfigViewBuilder) to build a deep link
        /// to this task's scheduling page. Previously this was
        /// `nameof(Name)`, which evaluates to the literal string "Name" -
        /// not a useful or stable identifier.
        /// </summary>
        public const string TaskKey = "GuestStarCleanerTask";

        private readonly ILibraryManager libraryManager;
        private readonly ILogger log;

        public string Name => "Guest Star Cleaner";

        public string Key => TaskKey;

        public string Description => "Remove Duplicate Guest Stars";

        public string Category => "GinjaNinja Tools";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public PluginScheduledTask(ILibraryManager libraryManager, ILogManager logManager)
        {
            this.libraryManager = libraryManager;
            this.log = logManager.GetLogger(Plugin.Instance.Name);
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance.Configuration;
            if (!config.EnableGSCleaner)
            {
                this.log.Info("Guest Star Cleaner is not enabled in plugin configuration: exiting now");
                return Task.CompletedTask;
            }

            this.log.Info("Guest Star Cleaner starting");

            var seriesList = GetSeries();
            if (seriesList.Count == 0)
            {
                this.log.Info("Guest Star Cleaner: no series found in library");
                progress.Report(100);
                return Task.CompletedTask;
            }

            this.log.Info("Guest Star Cleaner: {0} series to process", seriesList.Count);

            // A plain foreach here (not List<T>.ForEach) is deliberate: this
            // work is entirely synchronous library-manager calls, so a
            // normal loop processes one series fully before moving to the
            // next. Previously this used series?.ForEach(async item => ...),
            // which is a well-known trap - ForEach takes an Action<T>, so
            // the async lambda's returned Task was discarded. Every
            // series's processing was fired off concurrently and
            // unawaited, which both interleaved/garbled the per-series log
            // output and caused the progress counter to be incremented and
            // reported as soon as each fire-and-forget lambda was merely
            // *started*, not once it finished - hence progress jumping to
            // 100% almost immediately while work was still in flight.
            // Tracks whether a duplicate-person merge has already been
            // performed this task run. While Test Mode is on, only one
            // merge round is evaluated per run so results can be reviewed
            // incrementally - see DuplicatePersonMerger.EvaluateAndRepair.
            bool mergePerformedThisRun = false;

            for (int i = 0; i < seriesList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ProcessSeries(seriesList[i], config, ref mergePerformedThisRun);

                double percentComplete = 100.0 * (i + 1) / seriesList.Count;
                progress.Report(percentComplete);
            }

            if (config.DuplicatePersonMergeMode != Configuration.DuplicatePersonMergeMode.Off)
            {
                this.log.Info(
                    "Guest Star Cleaner: [DuplicatePersonDetection] {0}",
                    mergePerformedThisRun
                        ? (config.EnableGSTestmode
                            ? "Testmode On: 1 merge evaluated this run (capped while testing) - see above for detail"
                            : "1 merge performed this run - see above for detail")
                        : "No merges performed this run");
            }

            this.log.Info("Guest Star Cleaner finished");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes a single series: compares each episode's people against
        /// the series-level people and removes (or, in test mode, reports)
        /// any guest star already credited as a series-level actor.
        /// </summary>
        private void ProcessSeries(BaseItem series, Configuration.PluginConfiguration config, ref bool mergePerformedThisRun)
        {
            var seriesQuery = new InternalPeopleQuery
            {
                ItemIds = new[] { series.InternalId },
                EnableIds = true,
                EnableProviderIds = true,
            };

            var seriesPeople = this.libraryManager.GetItemPeople(seriesQuery);
            var episodes = GetEpisodes(series);

            int episodesWithDuplicates = 0;

            foreach (var episode in episodes)
            {
                if (ProcessEpisode(episode, series, seriesPeople, config, ref mergePerformedThisRun))
                {
                    episodesWithDuplicates++;
                }
            }

            if (episodesWithDuplicates == 0)
            {
                this.log.Info("Series '{0}': clean, no duplicates", series.Name);
            }
            else if (config.EnableGSTestmode)
            {
                this.log.Info(
                    "Testmode On: Series '{0}': {1} episode(s) with duplicates found - enable Debug log for details, turn off Testmode to remove from Emby",
                    series.Name,
                    episodesWithDuplicates);
            }
            else
            {
                this.log.Info(
                    "Series '{0}': {1} episode(s) with duplicates cleaned - enable Debug log for details",
                    series.Name,
                    episodesWithDuplicates);
            }
        }

        /// <summary>
        /// Processes a single episode against its series' people.
        /// Returns true if the episode had one or more duplicate guest
        /// stars (regardless of whether test mode prevented removal).
        /// </summary>
        private bool ProcessEpisode(
            BaseItem episode,
            BaseItem series,
            List<PersonInfo> seriesPeople,
            Configuration.PluginConfiguration config,
            ref bool mergePerformedThisRun)
        {
            var episodeQuery = new InternalPeopleQuery
            {
                ItemIds = new[] { episode.InternalId },
                EnableIds = true,
                EnableProviderIds = true,
            };

            var episodePeople = this.libraryManager.GetItemPeople(episodeQuery);

            var duplicatePeople = (
                from ep in episodePeople
                where seriesPeople.Any(sp =>
                    sp.Id == ep.Id &&
                    ((ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) ||
                     (ep.Type == PersonType.Actor && sp.Type == PersonType.Actor)))
                select ep).ToList();

            var checkPeople = (
                from ep in episodePeople
                where seriesPeople.Any(sp => sp.Name == ep.Name && sp.Id != ep.Id)
                select ep).ToList();

            foreach (var check in checkPeople)
            {
                var matchingSeriesPerson = seriesPeople.First(sp => sp.Name == check.Name && sp.Id != check.Id);

                bool merged = DuplicatePersonMerger.EvaluateAndRepair(
                    this.log,
                    this.libraryManager,
                    config,
                    series,
                    episode,
                    matchingSeriesPerson,
                    check,
                    mergePerformedThisRun);

                if (merged)
                {
                    mergePerformedThisRun = true;
                }
            }

            if (duplicatePeople.Count == 0)
            {
                return false;
            }

            foreach (var guestStar in duplicatePeople)
            {
                if (config.EnableGSTestmode)
                {
                    this.log.Debug(
                        "Testmode On: would remove duplicate person '{0}' (Type={1}) from S{2}E{3} - '{4}'",
                        guestStar.Name,
                        guestStar.Type,
                        episode.ParentIndexNumber?.ToString("D2") ?? "??",
                        episode.IndexNumber?.ToString("D2") ?? "??",
                        episode.Name);
                }
                else
                {
                    RemovePerson(guestStar, episode);

                    this.log.Debug(
                        "Removed duplicate person '{0}' (Type={1}) from S{2}E{3} - '{4}'",
                        guestStar.Name,
                        guestStar.Type,
                        episode.ParentIndexNumber?.ToString("D2") ?? "??",
                        episode.IndexNumber?.ToString("D2") ?? "??",
                        episode.Name);
                }
            }

            if (config.EnableGSTestmode)
            {
                this.log.Info(
                    "Testmode On: {0} duplicate guest star(s) found (not removed) in S{1}E{2} - '{3}'",
                    duplicatePeople.Count,
                    episode.ParentIndexNumber?.ToString("D2") ?? "??",
                    episode.IndexNumber?.ToString("D2") ?? "??",
                    episode.Name);
            }
            else
            {
                this.log.Info(
                    "Removed {0} duplicate guest star(s) from S{1}E{2} - '{3}'",
                    duplicatePeople.Count,
                    episode.ParentIndexNumber?.ToString("D2") ?? "??",
                    episode.IndexNumber?.ToString("D2") ?? "??",
                    episode.Name);
            }

            return true;
        }

        private List<BaseItem> GetEpisodes(BaseItem series)
        {
            var query = new InternalItemsQuery
            {
                Recursive = true,
                ParentIds = new[] { series.InternalId },
                IncludeItemTypes = new[] { nameof(Episode) },
            };

            try
            {
                return this.libraryManager.GetItemList(query).ToList();
            }
            catch (Exception ex)
            {
                this.log.ErrorException($"Error retrieving episodes for series '{series.Name}'", ex);
                return new List<BaseItem>();
            }
        }

        private List<BaseItem> GetSeries()
        {
            try
            {
                var query = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] { nameof(Series) },
                };

                return this.libraryManager.GetItemList(query).ToList();
            }
            catch (Exception ex)
            {
                this.log.ErrorException("Error retrieving series list", ex);
                return new List<BaseItem>();
            }
        }

        private void RemovePerson(PersonInfo person, BaseItem episode)
        {
            var removeQuery = new InternalPeopleQuery
            {
                ItemIds = new[] { episode.InternalId },
                EnableIds = true,
            };

            var currentPeople = this.libraryManager.GetItemPeople(removeQuery);

            for (int i = currentPeople.Count - 1; i >= 0; i--)
            {
                if (currentPeople[i].Id == person.Id && currentPeople[i].Type == person.Type)
                {
                    currentPeople.RemoveAt(i);
                }
            }

            this.libraryManager.UpdatePeople(episode, currentPeople, false);
        }

        // Task Triggers - currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }
    }
}
