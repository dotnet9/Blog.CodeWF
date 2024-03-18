﻿using Encoder = CodeWF.Web.Configuration.Encoder;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using JsonSerializer = System.Text.Json.JsonSerializer;
using MetaWeblogService = CodeWF.Web.MetaWeblogService;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
List<CultureInfo> cultures = new[] { "en-US", "zh-Hans", "zh-Hant" }.Select(p => new CultureInfo(p)).ToList();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WriteParameterTable();

builder.Logging.AddAzureWebAppDiagnostics();
builder.Configuration.AddJsonFile("manifesticons.json", false, true);

ConfigureServices(builder.Services);

WebApplication app = builder.Build();

await app.DetectChina();
await app.InitStartUp();

ConfigureMiddleware();

app.Run();

void ConfigureServices(IServiceCollection services)
{
    AppDomain.CurrentDomain.Load("CodeWF.Core");
    AppDomain.CurrentDomain.Load("CodeWF.FriendLink");
    AppDomain.CurrentDomain.Load("CodeWF.Theme");
    AppDomain.CurrentDomain.Load("CodeWF.Configuration");
    AppDomain.CurrentDomain.Load("CodeWF.Data");

    services.AddMediatR(config => config.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()));
    services.AddOptions()
        .AddHttpContextAccessor();
    services.AddApplicationInsightsTelemetry();

    services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(20);
        options.Cookie.HttpOnly = true;
    }).AddSessionBasedCaptcha(options => options.FontStyle = FontStyle.Bold);

    services.AddLocalization(options => options.ResourcesPath = "Resources");
    services.AddControllers(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .ConfigureApiBehaviorOptions(ConfigureApiBehavior.BlogApiBehavior);
    services.AddRazorPages()
        .AddDataAnnotationsLocalization(options =>
            options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(Program)))
        .AddRazorPagesOptions(options =>
        {
            options.Conventions.AddPageRoute("/Admin/Post", "admin");
            options.Conventions.AuthorizeFolder("/Admin");
            options.Conventions.AuthorizeFolder("/Settings");
        });

    // Fix Chinese character being encoded in HTML output
    services.AddSingleton(Encoder.CodeWfHtmlEncoder);

    services.AddAntiforgery(options =>
    {
        const string csrfName = "CSRF-TOKEN-CODEWF";
        options.Cookie.Name = $"X-{csrfName}";
        options.FormFieldName = $"{csrfName}-FORM";
        options.HeaderName = "XSRF-TOKEN";
    });

    services.Configure<RequestLocalizationOptions>(options =>
    {
        options.DefaultRequestCulture = new RequestCulture("en-US");
        options.SupportedCultures = cultures;
        options.SupportedUICultures = cultures;
    });

    services.Configure<RouteOptions>(options =>
    {
        options.LowercaseUrls = true;
        options.LowercaseQueryStrings = true;
        options.AppendTrailingSlash = false;
    });

    services.AddTransient<IPasswordGenerator, DefaultPasswordGenerator>();

    services.AddHealthChecks();
    services.AddPingback()
        .AddSyndication()
        .AddInMemoryCacheAside()
        .AddMetaWeblog<MetaWeblogService>()
        .AddScoped<ValidateCaptcha>()
        .AddScoped<ITimeZoneResolver, BlogTimeZoneResolver>()
        .AddBlogConfig()
        .AddBlogAuthenticaton(builder.Configuration)
        .AddImageStorage(builder.Configuration,
            options => options.ContentRootPath = builder.Environment.ContentRootPath)
        .Configure<List<ManifestIcon>>(builder.Configuration.GetSection("ManifestIcons"));

    services.AddEmailSending();
    services.AddContentModerator(builder.Configuration);

    var dbType =
        (DatabaseType)Enum.Parse(typeof(DatabaseType), builder.Configuration.GetConnectionString("DatabaseType")!,
            true);
    var connStr = builder.Configuration.GetConnectionString("CodeWFDatabase");
    switch (dbType)
    {
        case DatabaseType.MySql:
            services.AddMySqlStorage(connStr!);
            break;
        case DatabaseType.PostgreSQL:
            services.AddPostgreSqlStorage(connStr!);
            break;
        case DatabaseType.SqlServer:
            services.AddSqlServerStorage(connStr!);
            break;
        default:
            services.AddSQLiteStorage(connStr!);
            break;
    }
}

void ConfigureMiddleware()
{
    bool useXFFHeaders = app.Configuration.GetSection("ForwardedHeaders:Enabled").Get<bool>();
    if (useXFFHeaders)
    {
        UseSmartXFFHeader(app);
    }

    if (!app.Environment.IsProduction())
    {
        app.Logger.LogWarning(
            $"Running in environment: {app.Environment.EnvironmentName}. Application Insights disabled.");

        TelemetryConfiguration tc = app.Services.GetRequiredService<TelemetryConfiguration>();
        tc.DisableTelemetry = true;
        TelemetryDebugWriter.IsTracingDisabled = true;
    }

    app.UseCustomCss(options => options.MaxContentLength = 10240);
    app.UseManifest(options => options.ThemeColor = "#333333");
    app.UseRobotsTxt();

    app.UseOpenSearch(options =>
    {
        options.RequestPath = "/opensearch";
        options.IconFileType = "image/png";
        options.IconFilePath = "/favicon-16x16.png";
    });

    IBlogConfig bc = app.Services.GetRequiredService<IBlogConfig>();

    app.UseWhen(
        _ => bc.AdvancedSettings.EnableFoaf,
        appBuilder => appBuilder.UseMiddleware<FoafMiddleware>()
    );

    app.UseWhen(
        _ => bc.AdvancedSettings.EnableMetaWeblog,
        appBuilder => appBuilder.UseMiddleware<RSDMiddleware>().UseMetaWeblog("/metaweblog")
    );

    app.UseWhen(
        ctx => bc.AdvancedSettings.EnableSiteMap && ctx.Request.Path == "/sitemap.xml",
        appBuilder => appBuilder.UseMiddleware<SiteMapMiddleware>()
    );

    app.UseMiddleware<PoweredByMiddleware>();
    app.UseMiddleware<DNTMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseStatusCodePages(ConfigureStatusCodePages.Handler).UseExceptionHandler("/error");
        // app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRequestLocalization(new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("en-US"),
        SupportedCultures = cultures,
        SupportedUICultures = cultures
    });

    RewriteOptions options = new RewriteOptions().AddRedirect("(.*)/$", "$1", 301);
    app.UseRewriter(options);

    app.UseStaticFiles();
    app.UseSession().UseCaptchaImage(p =>
    {
        p.RequestPath = "/captcha-image";
        p.ImageHeight = 36;
        p.ImageWidth = 100;
    });

    app.UseRouting();
    app.UseAuthentication().UseAuthorization();

    app.MapHealthChecks("/ping", new HealthCheckOptions { ResponseWriter = ConfigureEndpoints.WriteResponse });
    app.MapControllers();
    app.MapRazorPages();
}

void UseSmartXFFHeader(WebApplication webApplication)
{
    ForwardedHeadersOptions fho = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };

    // ASP.NET Core always use the last value in XFF header, which is AFD's IP address
    // Need to set as `X-Azure-ClientIP` as workaround
    // https://learn.microsoft.com/en-us/azure/frontdoor/front-door-http-headers-protocol
    string? headerName = webApplication.Configuration["ForwardedHeaders:HeaderName"];
    if (!string.IsNullOrWhiteSpace(headerName))
    {
        // RFC 7230
        if (headerName.Length > 40 || !Helper.IsValidHeaderName(headerName))
        {
            app.Logger.LogWarning($"XFF header name '{headerName}' is invalid, it will not be applied");
        }
        else
        {
            fho.ForwardedForHeaderName = headerName;
        }
    }

    string[]? knownProxies = webApplication.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
    if (knownProxies is { Length: > 0 })
    {
        // Fix docker deployments on Azure App Service blows up with Azure AD authentication
        // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-6.0
        // "Outside of using IIS Integration when hosting out-of-process, Forwarded Headers Middleware isn't enabled by default."
        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
        {
            // Fix #712
            // Adding KnownProxies will make Azure App Service boom boom with Azure AD redirect URL
            // Result in `https` incorrectly written into `http` and make `/signin-oidc` url invalid.
            webApplication.Logger.LogWarning("Running in Docker, skip adding 'KnownProxies'.");
        }
        else
        {
            fho.ForwardLimit = null;
            fho.KnownProxies.Clear();

            foreach (string ip in knownProxies)
            {
                fho.KnownProxies.Add(IPAddress.Parse(ip));
            }

            webApplication.Logger.LogInformation("Added known proxies ({0}): {1}",
                knownProxies.Length,
                JsonSerializer.Serialize(knownProxies));
        }
    }
    else
    {
        // Fix deployment on AFD would not get the correct client IP address because it doesn't trust network other than localhost by default
        // Add this can make ASP.NET Core read forward headers from any network with a potential security issue
        // Attackers can hide their IP by sending a fake header
        // This is OK because CodeWF is just a blog, nothing to hack, let it be
        fho.KnownNetworks.Add(new IPNetwork(IPAddress.Any, 0));
        fho.KnownNetworks.Add(new IPNetwork(IPAddress.IPv6Any, 0));
    }

    webApplication.UseForwardedHeaders(fho);
}