using System.ComponentModel;
using Emby.GuestStarCleaner.Configuration;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;

namespace Emby.GuestStarCleaner.UI.Config
{
    /// <summary>
    /// On-screen view-model for the config page. Built fresh from
    /// PluginConfiguration on every render by ConfigViewBuilder, and never
    /// used as the persisted object itself - see ConfigPageView.HandleSave,
    /// which copies only the real settings back onto
    /// Plugin.Instance.Configuration before calling SaveConfiguration().
    /// </summary>
    public class ConfigUI : EditableOptionsBase
    {
        public override string EditorTitle => "Guest Star Cleaner - Configuration";

        public override string EditorDescription =>
            "Removes duplicate Guest Star credits from episodes when the same person is already credited at the series level.";

        public CaptionItem GeneralHeading { get; set; } = new CaptionItem("General");

        [DisplayName("Enable Plugin")]
        [Description("When disabled, the scheduled task exits immediately without processing any items.")]
        [AutoPostBack("updateconfig", nameof(EnableGSCleaner))]
        public bool EnableGSCleaner { get; set; } = true;

        [DisplayName("Test Mode")]
        [Description("When enabled, duplicates are logged but not removed. Turn off to actually remove them from Emby.")]
        [AutoPostBack("updateconfig", nameof(EnableGSTestmode))]
        public bool EnableGSTestmode { get; set; } = true;

        public CaptionItem DuplicatePersonHeading { get; set; } = new CaptionItem("Duplicate Person Repair");

        [DisplayName("Duplicate Person Merge Mode")]
        [Description("When a series and episode credit share a name but different Emby person Ids, this controls whether the plugin automatically repoints media items onto one canonical Id. Always logged under [DuplicatePersonDetection] regardless of this setting. While Test Mode is on, no changes are made. While Test Mode is off, merges are capped at 1 per task run until this feature has been tested and confirmed.")]
        [AutoPostBack("updateconfig", nameof(DuplicatePersonMergeMode))]
        public DuplicatePersonMergeMode DuplicatePersonMergeMode { get; set; } = DuplicatePersonMergeMode.Off;

        public GenericItemList ScheduledTaskLink { get; set; } = new GenericItemList();

        public GenericItemList ForumLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Community Forum",
                SecondaryText = "Issues, Suggestions and Updates",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "https://emby.media/community/topic/115611-ginjaninja-tools-guest-star-cleaner-remove-guest-stars-from-episodes-if-credited-on-series/#comment-1218848",
                HyperLinkTargetExternal = true
            }
        };

        public GenericItemList GithubLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Github repository",
                SecondaryText = "",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "https://github.com/ginjaninja1/Emby.GuestStarCleaner",
                HyperLinkTargetExternal = true
            }
        };
    }
}