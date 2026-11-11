using Microsoft.AspNetCore.Mvc;

namespace Stockly.Api.Infrastructure;

internal static class FileResults
{
    public static FileContentResult Csv(byte[] content, string name, DateOnly today) =>
        new(content, "text/csv; charset=utf-8") { FileDownloadName = $"{name}-{today:yyyyMMdd}.csv" };
}
