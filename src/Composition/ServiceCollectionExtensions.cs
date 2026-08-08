using System;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;
using PentaGrammata.Services;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Composition;

/// <summary>
/// Composition-root helpers that register the application's service collection by kind —
/// infrastructure, stores, services, view models — so each new dependency has an obvious,
/// discoverable home. Buckets are ordered stores → services → view models as a reading
/// aid matching the dependency direction. Declaration order is not required for correct
/// resolution because every service type is registered exactly once.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Plumbing with no domain identity: filesystem paths, HTTP, the update checker, the
        /// audio/morse generator stack, and the application window context.
        /// </summary>
        public void AddInfrastructure()
        {
            services.AddSingleton<IWindowContext, WindowContext>();
            services.AddSingleton<IAudioPlayer>(_ => AudioPlayerFactory.Create());
            services.AddSingleton<INoiseGeneratorFactory, NoiseGeneratorFactory>();
            services.AddSingleton<IMorsePlayer, MorsePlayer>();
            services.AddSingleton<IMorseGenerator, MorseGenerator>();
            services.AddSingleton<IAppPaths, AppPaths>();
            services.AddSingleton(_ => new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) });
            services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        }

        /// <summary>
        /// Persistence surfaces accessed through the service layer.
        /// </summary>
        public void AddStores()
        {
            services.AddSingleton<IWindowSizeStore, WindowSizeStore>();
            services.AddSingleton<IConfigurationStore, ConfigurationStore>();
            services.AddSingleton<IPracticeResultStatisticsStore, PracticeResultStatisticsStore>();
        }

        /// <summary>
        /// Domain and presentation services that sit between the view models and the stores.
        /// </summary>
        public void AddServices()
        {
            services.AddSingleton<IWindowSizeService, WindowSizeService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IPracticeSettingsValidator, PracticeSettingsValidator>();
            services.AddSingleton<IPracticeResultEvaluator, PracticeResultEvaluator>();
            services.AddSingleton<IDynamicWpmAdjuster, DynamicWpmAdjuster>();
            services.AddSingleton<IPracticeResultStatisticsService, PracticeResultStatisticsService>();
            services.AddSingleton<IConfusionAnalysisService, ConfusionAnalysisService>();
            services.AddSingleton<IPracticeStatisticsExporter, PracticeStatisticsCsvExporter>();
            services.AddSingleton<IPracticeController, PracticeController>();
            services.AddSingleton<IInfoDialogService, InfoDialogService>();
            services.AddSingleton<IMorseSettingsDialogService, MorseSettingsDialogService>();
            services.AddSingleton<IUiSettingsDialogService, UiSettingsDialogService>();
            services.AddSingleton<IPracticeResultWindowService, PracticeResultWindowService>();
            services.AddSingleton<IAboutDialogService, AboutDialogService>();
            services.AddSingleton<ITrendsDialogService, TrendsDialogService>();
            services.AddSingleton<IConfusionsDialogService, ConfusionsDialogService>();
        }

        /// <summary>
        /// View-model composition surface: the factory that constructs dialog view models and
        /// the application's root view model.
        /// </summary>
        public void AddViewModels()
        {
            services.AddSingleton<IDialogViewModelFactory, DialogViewModelFactory>();
            services.AddSingleton<PracticeViewModel>();
            services.AddSingleton<MainWindowViewModel>();
        }
    }
}
