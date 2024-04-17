﻿using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using RGPopup.Maui;
using RGPopup.Maui.Extensions;

namespace RGPopup.Samples
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiRGPopup()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
		    builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}