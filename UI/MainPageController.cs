using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using Emby.GuestStarCleaner.UIBaseClasses;

namespace Emby.GuestStarCleaner.UI
{
    /// <summary>
    /// This plugin has exactly one config page, so unlike a multi-tab
    /// controller this does not implement IHasTabbedUIPages - there are no
    /// tab pages to expose.
    /// </summary>
    internal class MainPageController : ControllerBase
    {
        private readonly PluginInfo pluginInfo;
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

        public MainPageController(
            PluginInfo pluginInfo,
            IServerApplicationHost applicationHost,
            ILogger logger)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.applicationHost = applicationHost;
            this.logger = logger;

            this.PageInfo = new PluginPageInfo
            {
                Name = "GuestStarCleaner",
                EnableInMainMenu = false,
                DisplayName = "Guest Star Cleaner",
                MenuIcon = "star",
                IsMainConfigPage = true
            };
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new ConfigPageView(
                this.pluginInfo,
                this.applicationHost,
                this.logger);

            return Task.FromResult(view);
        }
    }
}
