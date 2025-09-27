using System;

namespace MauiAppBrownianMotion.Utilities
{
    /// <summary>
    /// Provides access to the application's service provider before the MAUI handler pipeline is available.
    /// </summary>
    public static class ServiceHelper
    {
        /// <summary>
        /// Gets the application's root service provider.
        /// </summary>
        public static IServiceProvider? Services { get; private set; }

        /// <summary>
        /// Initializes the helper with the application's root service provider.
        /// </summary>
        /// <param name="serviceProvider">The application's service provider.</param>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            Services = serviceProvider;
        }
    }
}
