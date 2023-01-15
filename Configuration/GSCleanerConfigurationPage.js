define([
        "loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle",
        "emby-collapse"
    ],
    function(loading, dialogHelper, mainTabsManager) {

        const pluginId = "DD652519-2D16-46C4-B5B5-D697FBCF425C";

        function getTabs() {
            return [{
                    href: Dashboard.getConfigurationPageUrl('GSCleanerConfigurationPage'),
                    name: 'Guest Star Cleaner'
                }
                /*,
                                {
                                    href: Dashboard.getConfigurationPageUrl('PluginTab2ConfigurationPage'),
                                    name: 'PluginTab 2'
                                },
                                {
                                    href: Dashboard.getConfigurationPageUrl('PluginTab3ConfigurationPage'),
                                    name: 'PluginTab 3'
                                }*/
            ];
        }
        function LoadConfig(view, config) {

            ApiClient.getPluginConfiguration(pluginId).then(function(config) {

                view.querySelector(".chkEnableGSCleaner").checked = config.EnableGSCleaner;
            });
        }

        return function(view) {
            view.addEventListener('viewshow', async() => {

                loading.show();

                mainTabsManager.setTabs(this, 0, getTabs);

                var config = await ApiClient.getPluginConfiguration(pluginId);
                LoadConfig(view, config);

                loading.hide();

                document.querySelector('.pageTitle').innerHTML = "Guest Star Cleaner" + '<a is="emby-linkbutton" class="raised raised-mini emby-button" target="_blank" href=""><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';

                var enableGSCleaner = view.querySelector(".chkEnableGSCleaner");
                enableGSCleaner.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableGSCleaner = enableGSCleaner.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                                Dashboard.processPluginConfigurationUpdateResult(r);
                            });
                        });
                    });
            });
        };
    });