using MediaBrowser.Model.Plugins;

namespace Emby.GuestStarCleaner.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        //User Configuration Files
        public bool EnableGSCleaner { get; set; }
        public bool EnableGSTestmode { get; set; }

        public PluginConfiguration()
        {
            //add default values here to use
            EnableGSCleaner = true;
            EnableGSTestmode = true;

        }
    }
}
