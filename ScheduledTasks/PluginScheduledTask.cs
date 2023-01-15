using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.GuestStarCleaner.ScheduledTasks
{
    //Use this section if you need to have Scheduled tasks run
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager LibraryManager;

        private readonly ILogger _log;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IUserDataManager _userDataManager;
        private IHttpClient _httpClient;
        private ISyncProvider syncProvider;

        public string Name => "Guest Star Cleaner";

        public string Key => nameof(Name);

        public string Description => "Remove Duplicate Guest Stars";

        public string Category => "GinjaNinja Tools";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        //Constructor
        public PluginScheduledTask(ILibraryManager libraryManager, ILogManager logManager, IServerApplicationHost serverApplicationHost, IHttpClient httpClient)
        {
            LibraryManager = libraryManager;
            _serverApplicationHost = serverApplicationHost;
            _httpClient = httpClient;
            _log = logManager.GetLogger(Plugin.Instance.Name);
        }

        //progressBar fields
        private double _totalProgress;

        //Get Library Item fields
        private BaseItem[] _itemsInLibraries;
        private int _numberOfItemsInLibraries;


        //Task that will execute from the SheduleTask Menu
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _log.Info("Getting Started");
            await GetSeries();
            
            List<PersonInfo> seriesPeople = new List<PersonInfo>();
            List<PersonInfo> episodePeople = new List<PersonInfo>();

            foreach (BaseItem item in _itemsInLibraries)
            {
                _log.Info("Series {0} {1} {2}", item.InternalId, item.Name, item.Path);
                seriesPeople = LibraryManager.GetItemPeople(item);
                var episodes = await GetEpisodes(item);
                
                IEnumerable<PersonInfo> duplicatePeople = new List<PersonInfo>();
                foreach (BaseItem episode in episodes)
                {
                    episodePeople = LibraryManager.GetItemPeople(episode);
                    duplicatePeople = from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;
                }

                //duplicatePeople = (List<PersonInfo>) from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;

                foreach (var gstar in duplicatePeople)
                {
                    _log.Debug("Duplicate person found: {0} with type = {1} in Season {2}:Episode{3}", gstar.Name, gstar.Type.ToString(), item.Name, item.IndexNumber.ToString());
                }

                _totalProgress++;
                double dProgress = 100 * (_totalProgress / _numberOfItemsInLibraries);
                progress.Report(dProgress);
            }
        }

        private async Task<List<BaseItem>> GetEpisodes(BaseItem item)
        {
            var queryList = new InternalItemsQuery
            {
                Recursive = true,
                ParentIds = new []{ item.InternalId},
                IncludeItemTypes = new[] { nameof(Episode) },
            };

            List<BaseItem> episodeList = new List<BaseItem>();
            episodeList = LibraryManager.GetItemList(queryList).ToList();
            return episodeList;

        }

        private async Task GetSeries()
        {
            try
            {
                var queryList = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] {nameof(Series)},
                };

                _itemsInLibraries = LibraryManager.GetItemList(queryList);
                _numberOfItemsInLibraries = _itemsInLibraries.Length;
                _log.Info("Total No. of Series in Library {0}", _numberOfItemsInLibraries.ToString());
            }
            catch (Exception ex)
            {
                _log.Error("Error:", ex.ToString());
                return;
            }
        }

        //Task Triggers - Currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }


    }
}
