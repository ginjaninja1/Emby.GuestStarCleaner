using System;
using System.Collections.Generic;
using System.IO;
using Emby.GuestStarCleaner.Configuration;
using Emby.GuestStarCleaner.UI;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Serialization;

namespace Emby.GuestStarCleaner
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasUIPages
    {
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

        private List<IPluginUIPageController> pages;

        public Plugin(
            IServerApplicationHost applicationHost,
            ILogManager logManager)
            : base(
                applicationHost.Resolve<IApplicationPaths>(),
                applicationHost.Resolve<IXmlSerializer>())
        {
            this.applicationHost = applicationHost;
            this.logger = logManager.GetLogger(this.Name);

            Instance = this;
        }

        /// <summary>
        /// Configuration is accessed via Instance.Configuration /
        /// SaveConfiguration() - inherited from BasePlugin&lt;T&gt;, no
        /// custom store needed.
        /// </summary>
        public static Plugin Instance { get; private set; }

        public override string Name => "Guest Star Cleaner";

        public override string Description =>
            "Removes duplicate Guest Star credits from episodes when the same person is already credited at the series level.";

        public override Guid Id => new Guid("DD652519-2D16-46C4-B5B5-D697FBCF425C");

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public Stream GetThumbImage()
            => this.GetType()
                .Assembly
                .GetManifestResourceStream(this.GetType().Namespace + ".thumb.png");

        public IReadOnlyCollection<IPluginUIPageController> UIPageControllers
        {
            get
            {
                if (this.pages == null)
                {
                    this.pages = new List<IPluginUIPageController>
                    {
                        new MainPageController(
                            this.GetPluginInfo(),
                            this.applicationHost,
                            this.logger)
                    };
                }

                return this.pages.AsReadOnly();
            }
        }
    }
}
