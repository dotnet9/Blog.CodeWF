﻿using System.Linq.Expressions;

namespace CodeWF.Data.Exporting.Exporters;

public interface IExporter<T>
{
    Task<ExportResult> ExportData<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct);
}