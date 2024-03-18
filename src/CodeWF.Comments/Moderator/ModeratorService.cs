﻿namespace CodeWF.Comments.Moderator;

public interface IModeratorService
{
    public Task<string> Mask(string input);

    public Task<bool> Detect(params string[] input);
}

public class AzureFunctionModeratorService : IModeratorService
{
    private readonly bool _enabled;
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AzureFunctionModeratorService> _logger;
    private readonly string _provider;

    public AzureFunctionModeratorService(
        IHttpContextAccessor httpContextAccessor, ILogger<AzureFunctionModeratorService> logger, HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _provider = configuration["ContentModerator:Provider"]!.ToLower();
        _httpClient = httpClient;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrWhiteSpace(configuration["ContentModerator:ApiEndpoint"]))
        {
            _httpClient.BaseAddress = new Uri(configuration["ContentModerator:ApiEndpoint"]);
            _httpClient.DefaultRequestHeaders.Add("x-functions-key", configuration["ContentModerator:ApiKey"]);
            _enabled = true;
        }
        else
        {
            _logger.LogError("ContentModerator:ApiEndpoint is empty");
            _enabled = false;
        }
    }

    public async Task<string> Mask(string input)
    {
        if (!_enabled)
        {
            return input;
        }

        try
        {
            Payload payload = new Payload
            {
                OriginAspNetRequestId = _httpContextAccessor.HttpContext?.TraceIdentifier,
                Contents = new[] { new Content { Id = "0", RawText = input } }
            };

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/{_provider}/mask",
                new StringContent(JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"));

            response.EnsureSuccessStatusCode();

            ModeratorResponse? moderatorResponse = await response.Content.ReadFromJsonAsync<ModeratorResponse>();
            return moderatorResponse?.ProcessedContents?[0].ProcessedText ?? input;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
            return input;
        }
    }

    public async Task<bool> Detect(params string[] input)
    {
        if (!_enabled)
        {
            return false;
        }

        try
        {
            IEnumerable<Content> contents =
                input.Select(p => new Content { Id = Guid.NewGuid().ToString(), RawText = p });

            Payload payload = new Payload
            {
                OriginAspNetRequestId = _httpContextAccessor.HttpContext?.TraceIdentifier,
                Contents = contents.ToArray()
            };

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/{_provider}/detect",
                new StringContent(JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"));

            response.EnsureSuccessStatusCode();
            ModeratorResponse? moderatorResponse = await response.Content.ReadFromJsonAsync<ModeratorResponse>();
            return moderatorResponse?.Positive ?? false;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
            return false;
        }
    }
}