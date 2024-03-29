﻿using CodeWF.ImageStorage.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeWF.ImageStorage;

public class ImageStorageOptions
{
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
}

public static class ServiceCollectionExtensions
{
    private static readonly ImageStorageOptions Options = new();

    public static IServiceCollection AddImageStorage(
        this IServiceCollection services, IConfiguration configuration, Action<ImageStorageOptions> options)
    {
        options(Options);

        IConfigurationSection section = configuration.GetSection(nameof(ImageStorage));
        ImageStorageSettings? settings = section.Get<ImageStorageSettings>();
        services.Configure<ImageStorageSettings>(section);

        string? provider = settings.Provider?.ToLower();
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentNullException("Provider", "Provider can not be empty.");
        }

        switch (provider)
        {
            case "azurestorage":
                if (settings.AzureStorageSettings == null)
                {
                    throw new ArgumentNullException(nameof(settings.AzureStorageSettings),
                        "AzureStorageSettings can not be null.");
                }

                services.AddAzureStorage(settings.AzureStorageSettings);
                break;
            case "filesystem":
                if (string.IsNullOrWhiteSpace(settings.FileSystemPath))
                {
                    throw new ArgumentNullException(nameof(settings.FileSystemPath),
                        "FileSystemPath can not be null or empty.");
                }

                services.AddFileSystemStorage(settings.FileSystemPath);
                break;
            case "miniostorage":
                if (settings.MinioStorageSettings == null)
                {
                    throw new ArgumentNullException(nameof(settings.MinioStorageSettings),
                        "MinioStorageSettings can not be null.");
                }

                services.AddMinioStorage(settings.MinioStorageSettings);
                break;
            default:
                string msg = $"Provider {provider} is not supported.";
                throw new NotSupportedException(msg);
        }

        return services;
    }

    private static void AddAzureStorage(this IServiceCollection services, AzureStorageSettings settings)
    {
        string conn = settings.ConnectionString;
        string container = settings.ContainerName;
        services.AddSingleton(_ => new AzureBlobConfiguration(conn, container))
            .AddSingleton<IBlogImageStorage, AzureBlobImageStorage>()
            .AddScoped<IFileNameGenerator>(_ => new GuidFileNameGenerator(Guid.NewGuid()));
    }

    private static void AddFileSystemStorage(this IServiceCollection services, string fileSystemPath)
    {
        string fullPath = FileSystemImageStorage.ResolveImageStoragePath(fileSystemPath);
        services.AddSingleton(_ => new FileSystemImageConfiguration(fullPath))
            .AddSingleton<IBlogImageStorage, FileSystemImageStorage>()
            .AddScoped<IFileNameGenerator>(_ => new GuidFileNameGenerator(Guid.NewGuid()));
    }

    private static void AddMinioStorage(this IServiceCollection services, MinioStorageSettings settings)
    {
        services.AddSingleton<IBlogImageStorage, MinioBlobImageStorage>()
            .AddScoped<IFileNameGenerator>(_ => new GuidFileNameGenerator(Guid.NewGuid()))
            .AddSingleton(_ => new MinioBlobConfiguration(
                settings.EndPoint,
                settings.AccessKey,
                settings.SecretKey,
                settings.BucketName,
                settings.WithSSL));
    }
}