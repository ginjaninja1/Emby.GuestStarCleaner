using System;
using System.Collections.Generic;
using System.IO;
using Emby.GuestStarCleaner.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.GuestStarCleaner
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public static Plugin Instance { get; set; }

        //You will need to generate a new GUID and paste it here - Tools => Create GUID
        private Guid _id = new Guid("DD652519-2D16-46C4-B5B5-D697FBCF425C");
        //[Guid("9E13488E-664B-47D6-B7F1-374919FD70BB")] type 5
        public override string Name => "Guest Star Cleaner";

        public override string Description => "Removes duplicate Guest Stars if already in Series level";

        public override Guid Id => _id;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths,
            xmlSerializer)
        {
            Instance = this;
        }
        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;

        //Display Thumbnail image for Plugin Catalogue  - you will need to change build action for thumb.jpg to embedded Resource
        public Stream GetThumbImage()
        {
            Type type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.jpg");
        }

        //Web pages for Server UI configuration
        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {

            new PluginPageInfo
            {
                //html File
                Name = "GSCleanerConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.GSCleanerConfigurationPage.html",
                EnableInMainMenu = false,
                /*MenuSection = "server",*/
                //MenuIcon = "theaters"
            },
            new PluginPageInfo
            {
                //javascript file
                Name = "GSCleanerConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.GSCleanerConfigurationPage.js"
            },
        };





    }
}
