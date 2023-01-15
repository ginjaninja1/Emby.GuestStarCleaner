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
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.GuestStarCleaner.ScheduledTasks
{
    //Use this section if you need to have Scheduled tasks run
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager LibraryManager;
        private readonly IItemRepository _itemRepository;
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
        public PluginScheduledTask(IItemRepository itemRepository, ILibraryManager libraryManager, ILogManager logManager, IServerApplicationHost serverApplicationHost, IHttpClient httpClient)
        {
            LibraryManager = libraryManager;
            _itemRepository = itemRepository;
            _serverApplicationHost = serverApplicationHost;
            _httpClient = httpClient;
            _log = logManager.GetLogger(Plugin.Instance.Name);
        }

        //progressBar fields
        private double _totalProgress;

        //Get Library Item fields
        private BaseItem[] _Series;
        private int _numberOfSeries;


        //Task that will execute from the SheduleTask Menu
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {

            var config = Plugin.Instance.Configuration;
            if (!config.EnableGSCleaner) { _log.Info("GuestStar Cleaner is not enabled --- Exiting now"); return; }
            
            _log.Info("Getting Started");
            await GetSeries();

            List<PersonInfo> seriesPeople = new List<PersonInfo>();
            List<PersonInfo> episodePeople = new List<PersonInfo>();
             
           
            foreach (BaseItem item in _Series)
            {
                var seriesquery = new InternalPeopleQuery
                {
                    
                    ItemIds = new[] { item.InternalId },
                    EnableIds = true,
                };

                
                _log.Info("Series {0} {1} {2}", item.InternalId, item.Name, item.Path);
                seriesPeople = LibraryManager.GetItemPeople(seriesquery);
                /*
                foreach (PersonInfo person in seriesPeople)
                {
                    _log.Info("Series Person {0} {1} {2}", person.Id, person.Type, person.Name);
                }
                */
                var episodes = await GetEpisodes(item);               
               

                IEnumerable<PersonInfo> duplicatePeople = new List<PersonInfo>();
                IEnumerable<PersonInfo> duplicatePeopleRelaxed = new List<PersonInfo>();
                foreach (BaseItem episode in episodes)
                {
                    var episodequery = new InternalPeopleQuery
                    {

                        ItemIds = new[] { episode.InternalId },
                        EnableIds = true,
                    };

                    episodePeople = LibraryManager.GetItemPeople(episodequery);
                    /*
                    foreach (PersonInfo person in episodePeople)
                    {
                        _log.Info("Episode Person {0} {1} {2}", person.Id, person.Type, person.Name);
                    }
                    */
                    duplicatePeople = from ep in episodePeople where seriesPeople.Any(sp => sp.Id == ep.Id && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;
                    duplicatePeopleRelaxed = from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;
                    // var episodeFilePath = GetFileName(episode);

                    foreach (var gstar in duplicatePeople)
                    {
                        if (!config.enableGSTestmode)
                        {
                            _log.Debug("Duplicate person removed: {0} with type = {1} in Season {2}:Episode{3}", gstar.Name, gstar.Type.ToString(), episode.ParentIndexNumber.ToString(), episode.IndexNumber.ToString());

                            await RemovePerson(gstar, episode );
                        } else
                        {
                            _log.Debug("Duplicate person found: {0} with type = {1} in Season {2}:Episode{3}", gstar.Name, gstar.Type.ToString(), episode.ParentIndexNumber.ToString(), episode.IndexNumber.ToString());

                        }

                    }
                    // _log.Info("Episode filepath = {0}", episodeFilePath);

                }

                //duplicatePeople = (List<PersonInfo>) from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;

                



                _totalProgress++;
                double dProgress = 100 * (_totalProgress / _numberOfSeries);
                progress.Report(dProgress);
            }
        }

        private string GetFileName(BaseItem item)
        {
            string fileName = string.Empty;
            fileName = item.Path;
            return fileName;
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

                _Series = LibraryManager.GetItemList(queryList);
                _numberOfSeries = _Series.Length;
                _log.Info("Total No. of Series in Library {0}", _numberOfSeries.ToString());
            }
            catch (Exception ex)
            {
                _log.Error("Error:", ex.ToString());
                return;
            }
        }

        private async Task RemovePerson(PersonInfo person, BaseItem episode)
        {
            List<PersonInfo> ifPeople = new List<PersonInfo>();
            var removequery = new InternalPeopleQuery
            {

                ItemIds = new[] { episode.InternalId },
                EnableIds = true,
            };

            ifPeople = LibraryManager.GetItemPeople(removequery);
            ifPeople.Remove(person);
            LibraryManager.UpdatePeople(episode, ifPeople, false);


        }

        //Task Triggers - Currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();

        }


    }
}
