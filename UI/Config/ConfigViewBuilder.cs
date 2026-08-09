using System;
using System.Linq;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using Emby.GuestStarCleaner.Configuration;
using MediaBrowser.Model.Tasks;

namespace Emby.GuestStarCleaner.UI.Config
{
    /// <summary>
    /// Builds the on-screen ConfigUI from the persisted PluginConfiguration.
    /// Always returns a NEW ConfigUI instance - never hands back or mutates
    /// the persisted instance itself.
    /// </summary>
    internal static class ConfigViewBuilder
    {
        public static ConfigUI BuildDisplayConfig(
            PluginConfiguration persistedConfig,
            ITaskManager taskManager)
        {
            var myTaskWorker = taskManager.ScheduledTasks
                .FirstOrDefault(t => string.Equals(t.ScheduledTask.Key, ScheduledTasks.PluginScheduledTask.TaskKey, StringComparison.Ordinal));

            string hyperlinkUrl = myTaskWorker != null
                ? $"/scheduledtask?id={myTaskWorker.Id}"
                : "/scheduledtasks";

            return new ConfigUI
            {
                EnableGSCleaner = persistedConfig.EnableGSCleaner,
                EnableGSTestmode = persistedConfig.EnableGSTestmode,
                DuplicatePersonMergeMode = persistedConfig.DuplicatePersonMergeMode,

                ScheduledTaskLink = new GenericItemList
                {
                    new GenericListItem
                    {
                        PrimaryText = "Configure Scheduled Task",
                        SecondaryText = "Manage background execution rules and automation intervals",
                        Icon = IconNames.link,
                        Status = ItemStatus.Succeeded,
                        HyperLink = hyperlinkUrl,
                        HyperLinkTargetExternal = false
                    }
                }
            };
        }
    }
}
