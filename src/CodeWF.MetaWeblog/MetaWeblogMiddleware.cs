﻿namespace CodeWF.MetaWeblog;

public class MetaWeblogMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, string urlEndpoint)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<MetaWeblogMiddleware>();

    public async Task Invoke(HttpContext context, MetaWeblogService service)
    {
        if (context.Request.Method == "POST" &&
            context.Request.Path.StartsWithSegments(urlEndpoint) &&
            context.Request.ContentType.ToLower().Contains("text/xml"))
        {
            try
            {
                context.Response.ContentType = "text/xml";
                StreamReader rdr = new StreamReader(context.Request.Body);
                string xml = await rdr.ReadToEndAsync();
                _logger.LogInformation($"Request XMLRPC: {xml}");
                string result = await service.InvokeAsync(xml);
                _logger.LogInformation($"Result XMLRPC: {result}");
                await context.Response.WriteAsync(result, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read the content: {ex}");
            }

            return;
        }

        // Continue On
        await next.Invoke(context);
    }
}