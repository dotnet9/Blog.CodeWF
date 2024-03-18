﻿namespace CodeWF.Pingback;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPingback(this IServiceCollection services)
    {
        services.AddHttpClient<IPingSourceInspector, PingSourceInspector>()
            .ConfigureHttpClient(p => p.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<IPingbackWebRequest, PingbackWebRequest>();
        services.AddHttpClient<IPingbackSender, PingbackSender>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { Credentials = CredentialCache.DefaultNetworkCredentials });

        return services;
    }
}