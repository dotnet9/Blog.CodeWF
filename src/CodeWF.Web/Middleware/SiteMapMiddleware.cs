﻿namespace CodeWF.Web.Middleware;

public class SiteMapMiddleware(RequestDelegate next)
{
    public async Task Invoke(
        HttpContext httpContext,
        IBlogConfig blogConfig,
        ICacheAside cache,
        IRepository<PostEntity> postRepo,
        IRepository<PageEntity> pageRepo)
    {
        string? xml = await cache.GetOrCreateAsync(BlogCachePartition.General.ToString(), "sitemap", async _ =>
        {
            string url = Helper.ResolveRootUrl(httpContext, blogConfig.GeneralSettings.CanonicalPrefix, true, true);
            string data = await GetSiteMapData(url, postRepo, pageRepo, httpContext.RequestAborted);
            return data;
        });

        httpContext.Response.ContentType = "text/xml";
        await httpContext.Response.WriteAsync(xml, httpContext.RequestAborted);
    }

    private static async Task<string> GetSiteMapData(
        string siteRootUrl,
        IRepository<PostEntity> postRepo,
        IRepository<PageEntity> pageRepo,
        CancellationToken ct)
    {
        StringBuilder sb = new();

        XmlWriterSettings writerSettings = new() { Encoding = Encoding.UTF8, Async = true };
        await using (XmlWriter writer = XmlWriter.Create(sb, writerSettings))
        {
            await writer.WriteStartDocumentAsync();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            // Posts
            PostSitePageSpec spec = new();
            IReadOnlyList<Tuple<string, DateTime?, DateTime?>> posts = await postRepo
                .SelectAsync(spec,
                    p => new Tuple<string, DateTime?, DateTime?>(p.Slug, p.PubDateUtc, p.LastModifiedUtc), ct);

            foreach ((string slug, DateTime? pubDateUtc, DateTime? lastModifyUtc) in posts.OrderByDescending(p =>
                         p.Item2))
            {
                DateTime pubDate = pubDateUtc.GetValueOrDefault();

                writer.WriteStartElement("url");
                writer.WriteElementString("loc",
                    $"{siteRootUrl}/post/{pubDate.Year}/{pubDate.Month}/{pubDate.Day}/{slug.ToLower()}");
                writer.WriteElementString("lastmod", pubDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteElementString("changefreq", GetChangeFreq(pubDateUtc.GetValueOrDefault(), lastModifyUtc));
                await writer.WriteEndElementAsync();
            }

            // Pages
            IReadOnlyList<Tuple<DateTime, DateTime?, string, bool>> pages = await pageRepo.SelectAsync(page =>
                new Tuple<DateTime, DateTime?, string, bool>(
                    page.CreateTimeUtc,
                    page.UpdateTimeUtc,
                    page.Slug,
                    page.IsPublished), ct);

            foreach ((DateTime createdTimeUtc, DateTime? updateTimeUtc, string slug, bool isPublished) in pages.Where(
                         p => p.Item4))
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", $"{siteRootUrl}/page/{slug.ToLower()}");
                writer.WriteElementString("lastmod",
                    createdTimeUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteElementString("changefreq", GetChangeFreq(createdTimeUtc, updateTimeUtc));
                await writer.WriteEndElementAsync();
            }

            // Tag
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", $"{siteRootUrl}/tags");
            writer.WriteElementString("lastmod", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteElementString("changefreq", "weekly");
            await writer.WriteEndElementAsync();

            // Archive
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", $"{siteRootUrl}/archive");
            writer.WriteElementString("lastmod", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteElementString("changefreq", "monthly");
            await writer.WriteEndElementAsync();

            await writer.WriteEndElementAsync();
        }

        string xml = sb.ToString();
        return xml;
    }

    private static string GetChangeFreq(DateTime pubDate, DateTime? modifyDate)
    {
        if (modifyDate == null || modifyDate == pubDate)
        {
            return "monthly";
        }

        int lastModifyFromNow = (DateTime.UtcNow - modifyDate.Value).Days;
        switch (lastModifyFromNow)
        {
            case <= 60:
                {
                    int interval = Math.Abs((modifyDate.Value - pubDate).Days);

                    return interval switch
                    {
                        < 7 => "daily",
                        >= 7 and <= 14 => "weekly",
                        > 14 => "monthly"
                    };
                }
            case > 60:
                return "yearly";
        }
    }
}