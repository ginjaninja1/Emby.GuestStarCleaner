using System;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Emby.GuestStarCleaner.UIBaseClasses.Views;
using Emby.GuestStarCleaner.UI.Config;

namespace Emby.GuestStarCleaner.UI
{
    /// <summary>
    /// The plugin's single config page. Deliberately kept to just:
    /// construction, page settings, and command handling that reads/writes
    /// the persisted configuration via Plugin.Instance.
    ///
    ///   - PluginConfiguration : the persisted schema (no UI members)
    ///   - ConfigUI            : the on-screen view-model (never persisted)
    ///   - ConfigViewBuilder   : builds ConfigUI from PluginConfiguration
    /// </summary>
    internal class ConfigPageView : PluginPageView
    {
        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;
        private readonly ITaskManager taskManager;

        public ConfigPageView(
            PluginInfo pluginInfo,
            IServerApplicationHost applicationHost,
            ILogger logger)
            : base(pluginInfo.Id)
        {
            this.logger = logger;
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();
            this.taskManager = applicationHost.Resolve<ITaskManager>();
            this.ShowSave = false;
            this.ShowBack = false;
            this.AllowBack = false;
            RebuildContentData();
        }

        /// <summary>
        /// NOTE: ContentData is always a freshly-built ConfigUI display
        /// object, never Plugin.Instance.Configuration itself - that's what
        /// stops visual elements from ever being written to disk.
        /// </summary>
        private void RebuildContentData()
        {
            var config = Plugin.Instance.Configuration;
            this.ContentData = ConfigViewBuilder.BuildDisplayConfig(config, this.taskManager);
        }

        public override Task<IPluginUIView> OnSaveCommand(
            string itemId,
            string commandId,
            string data)
        {
            return RunCommand(itemId, commandId, data);
        }

        public override Task<IPluginUIView> RunCommand(
            string itemId,
            string commandId,
            string data)
        {
            if (!string.IsNullOrEmpty(data) && commandId == "updateconfig")
            {
                HandleSave(data);
                return Task.FromResult<IPluginUIView>(this);
            }

            return Task.FromResult<IPluginUIView>(this);
        }

        private void HandleSave(string data)
        {
            var config = Plugin.Instance.Configuration;

            try
            {
                // GenericUI posts back the entire rendered ConfigUI object.
                // Only the real settings on it are copied onto the
                // persisted PluginConfiguration instance - headings/links
                // from the incoming payload are discarded here.
                var incoming = this.jsonSerializer.DeserializeFromString<ConfigUI>(data);

                if (incoming != null)
                {
                    config.EnableGSCleaner = incoming.EnableGSCleaner;
                    config.EnableGSTestmode = incoming.EnableGSTestmode;

                    Plugin.Instance.SaveConfiguration();

                    this.logger.Info("Guest Star Cleaner configuration saved");
                }
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error saving Guest Star Cleaner configuration", ex);
            }

            RebuildContentData();
        }
    }
}
