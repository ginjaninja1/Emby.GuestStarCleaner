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

        /// <summary>
        /// Controls automatic repair of duplicate Person entities detected
        /// when a series-level person and an episode-level person share a
        /// name but have different Emby person Ids. See
        /// DuplicatePersonMergeMode for the safety tiers. Defaults to Off:
        /// duplicates are always logged under [DuplicatePersonDetection]
        /// regardless of this setting, but only repaired when a merge tier
        /// is selected.
        /// </summary>
        public DuplicatePersonMergeMode DuplicatePersonMergeMode { get; set; } = DuplicatePersonMergeMode.Off;
    }
}
