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
    //Use this section if you need to have Scheduled tasks run
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager LibraryManager;
        private readonly ILogger _log;

        public string Name => "Guest Star Cleaner";

        public string Key => nameof(Name);

        public string Description => "Remove Duplicate Guest Stars";

        public string Category => "GinjaNinja Tools";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        //Constructor
        public PluginScheduledTask(ILibraryManager libraryManager, ILogManager logManager)
        {
            LibraryManager = libraryManager;
            _log = logManager.GetLogger(Plugin.Instance.Name);
        }

        //progressBar fields
        private double _totalProgress;

        //Get Library Item fields
        private BaseItem[] _series;
        private int _numberOfSeries;


        //Task that will execute from the ScheduleTask Menu
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {

            var config = Plugin.Instance.Configuration;
            if (!config.EnableGSCleaner)
            {
                _log.Info("GuestStar Cleaner is not enabled --- Exiting now"); 
                return;
            }
            
            _log.Info("Guest Star Cleaner Initializing");
            await GetSeries();

            List<PersonInfo> seriesPeople = new List<PersonInfo>();
            List<PersonInfo> episodePeople = new List<PersonInfo>();
             
           
            foreach (BaseItem item in _series)
            {
                var seriesquery = new InternalPeopleQuery
                {
                    ItemIds = new[] { item.InternalId },
                    EnableIds = true,
                };

                
                _log.Info("Getting Series and Episode Person Info for {1} -- Id:{0} -- Path:{2}", item.InternalId, item.Name, item.Path);
                seriesPeople = LibraryManager.GetItemPeople(seriesquery);
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
                    
                    duplicatePeople = from ep in episodePeople where seriesPeople.Any(sp => sp.Id == ep.Id && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;
                    //duplicatePeopleRelaxed = from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor) select ep;

                    if (duplicatePeople.Count() == 0)
                    {
                        _log.Info("There are no Duplicate persons found in {0}", episode.Name);
                    }
                    else
                    {
                        foreach (var gstar in duplicatePeople)
                        {
                            if (!config.EnableGSTestmode)
                            {
                                await RemovePerson(gstar, episode);
                                _log.Info(
                                    "Test Mode is NOT enabled - Removed dupicate person:{0} with type = {1} in Season {2}:Episode{3}",
                                    gstar.Name, gstar.Type.ToString(), episode.ParentIndexNumber.ToString(),
                                    episode.IndexNumber.ToString());
                            }
                            else
                            {
                                _log.Info("Test Mode Enabled - No actors will be removed from Database");
                                _log.Info("Duplicate person found: {0} with type = {1} in Season {2}:Episode{3}",
                                    gstar.Name, gstar.Type.ToString(), episode.ParentIndexNumber.ToString(),
                                    episode.IndexNumber.ToString());
                            }
                        }
                    }
                }
                _totalProgress++;
                double dProgress = 100 * (_totalProgress / _numberOfSeries);
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

                _series = LibraryManager.GetItemList(queryList);
                _numberOfSeries = _series.Length;
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
            
            for (int i = ifPeople.Count - 1; i >= 0; i--)
            {
                if ((ifPeople[i].Id == person.Id) && (ifPeople[i].Type == person.Type))
                {
                    ifPeople.RemoveAt(i);
                }
            }
            //_log.Info("After EpID:{0} S:{1}E:{2} PeopleCount:{3}", episode.InternalId, episode.ParentIndexNumber, episode.IndexNumber, ifPeople.Count);
            LibraryManager.UpdatePeople(episode, ifPeople, false);


        }

        //Task Triggers - Currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();

        }


    }
}
