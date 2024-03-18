﻿namespace CodeWF.ImageStorage;

public interface IFileNameGenerator
{
    string Name { get; }

    string GetFileName(string fileName, string appendixName = "");
}