﻿namespace CodeWF.Data.Entities;

public class BlogAssetEntity
{
    public Guid Id { get; set; }

    public string? Base64Data { get; set; }

    public DateTime LastModifiedTimeUtc { get; set; }
}