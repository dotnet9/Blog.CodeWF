﻿namespace CodeWF.Pingback;

public interface IPingbackWebRequest
{
    Task<HttpResponseMessage> Send(Uri sourceUrl, Uri targetUrl, Uri url);
}

public class PingbackWebRequest : IPingbackWebRequest
{
    private readonly HttpClient _httpClient;

    public PingbackWebRequest(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-us");
    }

    public async Task<HttpResponseMessage> Send(Uri sourceUrl, Uri targetUrl, Uri url)
    {
        await using StringWriter sw = new StringWriter();
        await using XmlTextWriter writer = new XmlTextWriter(sw);
        writer.WriteStartDocument(true);
        writer.WriteStartElement("methodCall");
        writer.WriteElementString("methodName", "pingback.ping");
        writer.WriteStartElement("params");

        writer.WriteStartElement("param");
        writer.WriteStartElement("value");
        writer.WriteElementString("string", sourceUrl.ToString());
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("param");
        writer.WriteStartElement("value");
        writer.WriteElementString("string", targetUrl.ToString());
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();

        string pingXml = sw.ToString();
        HttpResponseMessage pingResponse =
            await _httpClient.PostAsync(url, new StringContent(pingXml, Encoding.ASCII, "text/xml"));

        return pingResponse;
    }
}