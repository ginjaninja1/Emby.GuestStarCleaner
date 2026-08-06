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
        //private BaseItem[] _series;
        private int _numberOfSeries;


        //Task that will execute from the ScheduleTask Menu
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {

            var config = Plugin.Instance.Configuration;
            if (!config.EnableGSCleaner)
            {
                _log.Info("GuestStar Cleaner is Not Enabled in Plugin Configuration: Exiting Now"); 
                return;
            }
            
            _log.Info("Guest Star Cleaner Initializing");
            
            List<BaseItem> series = await GetSeries();


            List<PersonInfo> seriesPeople = new List<PersonInfo>();
            List<PersonInfo> episodePeople = new List<PersonInfo>();


            //foreach (BaseItem item in _series)
            series?.ForEach(async item =>
            {
            var seriesquery = new InternalPeopleQuery
            {
                ItemIds = new[] { item.InternalId },
                EnableIds = true,
            };


            _log.Info("Getting Series and Episode Person Info: {1} - Id:{0} - {2}", item.InternalId, item.Name, item.Path);
            //_log.Info("Getting Series People");
            seriesPeople = LibraryManager.GetItemPeople(seriesquery);
            //_log.Info("Getting Episodes in Series");
            var episodes = await GetEpisodes(item);


            IEnumerable<PersonInfo> duplicatePeople = new List<PersonInfo>();
            IEnumerable<PersonInfo> checkPeople = new List<PersonInfo>();
            var duplicates = false;
            //_log.Info("Entering Episode Loop");
            //foreach (BaseItem episode in episodes)
            episodes?.ForEach(async episode =>
            {
                //_log.Info("looping Episode: S{0}E{1}", episode.ParentIndexNumber.Value.ToString("D2"),episode.IndexNumber.Value.ToString("D2"));
                var episodequery = new InternalPeopleQuery
                {
                    ItemIds = new[] { episode.InternalId },
                    EnableIds = true,
                };
                //_log.Info("Getting Episode People");
                episodePeople = LibraryManager.GetItemPeople(episodequery);

                duplicatePeople = from ep in episodePeople where seriesPeople.Any(sp => sp.Id == ep.Id && (ep.Type == PersonType.GuestStar && sp.Type == PersonType.Actor || ep.Type == PersonType.Actor && sp.Type == PersonType.Actor)) select ep;
                checkPeople = from ep in episodePeople where seriesPeople.Any(sp => sp.Name == ep.Name && sp.Id != ep.Id) select ep;






                if (duplicatePeople.Count() != 0)
                {



                    duplicates = true;
                    foreach (var gstar in duplicatePeople)
                    {


                        if (!config.EnableGSTestmode)
                        {
                            await RemovePerson(gstar, episode);
                            _log.Debug("Removed Dupicate Person: {0} with type = {1} in S{2}E{3} - {4}",
                                gstar.Name, gstar.Type.ToString(), episode.ParentIndexNumber.Value.ToString("D2"),
                                episode.IndexNumber.Value.ToString("D2"), item.Name);
                        }
                        else
                        {

                            _log.Debug("Testmode On: Ignored Duplicate Person: {0} with type = {1} in S{2}E{3} - {4}",
                                gstar.Name, gstar.Type.ToString(), episode.ParentIndexNumber.Value.ToString("D2"),
                                episode.IndexNumber.Value.ToString("D2"), item.Name);
                        }
                    }
                }
                else
                {
                    if (checkPeople.Count() != 0)
                    {
                        foreach (var gstar2 in checkPeople)
                        {
                            _log.Debug("Possible Provider Data Error: Check Person: {0} with type = {1} in S{2}E{3} - {4} on series/provider",
                                    gstar2.Name, gstar2.Type.ToString(), episode.ParentIndexNumber.Value.ToString("D2"),
                                    episode.IndexNumber.Value.ToString("D2"), item.Name);
                        }


                    }
                }
                if (!duplicates)
                {
                    _log.Info("No Duplicates Detected for Series: {0}", item.Name);


                }
                else
                {
                    if (!config.EnableGSTestmode)
                    {
                        _log.Info("Duplicates Removed for Series: {0} - Enable Debug Log for Details", item.Name);
                    }
                    else
                    {
                        _log.Info("Testmode On: Duplicates Detected for Series: {0} - Enable Debug Log for Details; Turn Off Testmode to Remove from Emby", item.Name);
                    }


                }

            });
            
                
                _totalProgress++;
                double dProgress = 100 * (_totalProgress / _numberOfSeries);
                progress.Report(dProgress);
            }
            );
        }

        private async Task<List<BaseItem>> GetEpisodes(BaseItem item)
        {
            var queryList = new InternalItemsQuery
            {
                Recursive = true,
                ParentIds = new []{ item.InternalId},
                IncludeItemTypes = new[] { nameof(Episode) },
            };


            
            try
            {
                
                return LibraryManager.GetItemList(queryList).ToList();
                //return episodeList;
            }
            catch (Exception ex)
            {
                _log.Error("Error:", ex.ToString());
                List<BaseItem> episodeList = new List<BaseItem>();
                return episodeList;

            }
            

        }

        private async Task<List<BaseItem>> GetSeries()
        {
            try
            {
                var queryList = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] {nameof(Series)},
                };

                var seriesList = LibraryManager.GetItemList(queryList).ToList();
                //_numberOfSeries = _series.Length;
                _numberOfSeries = seriesList.Count;
                //_log.Info("Total No. of Series in Library {0}", _numberOfSeries.ToString());
                _log.Info("Total No. of Series in Library {0}", seriesList.Count);
                return seriesList;
            }
            catch (Exception ex)
            {
                _log.Error("Error:", ex.ToString());
                List<BaseItem> empty = new List<BaseItem>();
                return empty;
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
