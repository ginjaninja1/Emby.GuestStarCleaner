using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Model.Extensions;
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
        private int _totalItems;

        //Get Library Item fields
        private BaseItem[] _itemsInLibraries;
        private int _numberOfItemsInLibraries;
        private object _itemsCount;
        private BaseItem[] _personinseries;


        //Task that will execute from the SheduleTask Menu
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            //Do work here for your Scheduled Task
            //
            _log.Info("Getting Started");
            await GetSeries();
            foreach (BaseItem item in _itemsInLibraries)
            {
                _log.Info("Series {0} {1} {2}", item.Id, item.Name, item.Path);
                List<Person> seriesPeople = new List<Person>();
                seriesPeople = LibraryManager.GetItemPeople(item);


            }
            

        }

        private async Task GetSeries()
        {
            
            try
            {
                var queryList = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] {"Series"},
                    
                };

                _itemsInLibraries = LibraryManager.GetItemList(queryList);
                _numberOfItemsInLibraries = _itemsInLibraries.Length;
                _log.Info("Total No. of Series in Library {0}", _numberOfItemsInLibraries.ToString());
            }
            catch (Exception ex)
            {
                _log.Error("Error");
                
                return;

            }


        }

        private async Task GetPeople()
        {
            _log.Info("Get People for each Series Item");
            

            var queryList = new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes= new[] {"People"},
                ParentIds = new [] { item.Id },
            };





        }

        //Task Triggers - Currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }


    }
}
