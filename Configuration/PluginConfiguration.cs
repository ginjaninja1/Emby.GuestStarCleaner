using MediaBrowser.Model.Plugins;

namespace Emby.GuestStarCleaner.Configuration
{
    /// <summary>
    /// The plugin's persisted settings - the ONLY class involved in
    /// persistence. Uses Emby's standard BasePlugin&lt;T&gt; mechanism:
    /// Plugin.Instance.Configuration / SaveConfiguration(), which
    /// serializes to XML automatically. This class has no UI/visual
    /// members - the config page builds a separate view-model, ConfigUI,
    /// fresh from this class every time it's shown. See
    /// Emby.GuestStarCleaner.UI.Config.ConfigViewBuilder.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableGSCleaner { get; set; } = true;

        public bool EnableGSTestmode { get; set; } = true;
    }
}
