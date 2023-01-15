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
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.GuestStarCleaner.ScheduledTasks
{
    //Use this section if you need to have Scheduled tasks run
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager LibraryManager;
        //private IItemRepository ItemRepository { get; }
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
            //ItemRepository = itemRepository;
            _serverApplicationHost = serverApplicationHost;
            _httpClient = httpClient;
            _log = logManager.GetLogger(Plugin.Instance.Name);
        }

        //progressBar fields
        private double _totalProgress;

        //Get Library Item fields
        private BaseItem[] _itemsInLibraries;
        private int _numberOfItemsInLibraries;


        //Task that will execute from the ScheduleTask Menu
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
                var removeList = new List<PersonInfo>();
                
                foreach (BaseItem episode in episodes)
                {
                    episodePeople = LibraryManager.GetItemPeople(episode);
                    var duplicatePeople = from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;
                    
                    //What we've done here is to get the list of series people and open up the person properties so we can make some comparisons
                    foreach (var person in seriesPeople)
                    {
                        //we also need to open up the duplicatePeople properties
                        foreach (var dupe in duplicatePeople)
                        {
                            //then we are gonna do some conditional logic, where if the names match and also they are a guest star in the episode but also an actor on the Series list, we need to add this to a new list....
                            //so we can then remove them from the list we are actually iterating thru in the next stage.
                            if (dupe.Name == person.Name && dupe.Type == PersonType.GuestStar && person.Type == PersonType.Actor)
                            {
                                _log.Debug("Dupe found: {0} with RoleType: {1}", dupe.Name, dupe.Type.ToString());
                                removeList.Add(dupe);
                            }
                        }
                    }

                    //Once we have the removeList we can now remove each person from the episodePeople list.
                    foreach (var peep in removeList)
                    {
                        episodePeople.Remove(peep);
                    }

                    _log.Debug("Episode Id = {0} -- SeriesId = {1}", episode.InternalId.ToString(), item.InternalId.ToString());

                    //Then we can update the people list for each episode with our new episodePeople list (which has the dupes removed).
                    //A library scan will need to be run by the user after this plugin has been run.

                    //TODO uncomment the next line under this one.  TEST on test library first
                    //LibraryManager.UpdatePeople(episode, episodePeople, false);
                }

                foreach (var epRole in episodePeople)
                {
                    _log.Debug("Episode Person Remaining : {0} with RoleType: {1}", epRole.Name, epRole.Type.ToString());

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
