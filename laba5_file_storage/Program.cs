
namespace FileStorageAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Включаем стандартные HTTP-ошибки
            builder.Services.AddProblemDetails();
            var app = builder.Build();
            app.UseStatusCodePages();

            // Инициализация корневой папки хранилища
            string storageRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");

            if (!Directory.Exists(storageRootPath))
            {
                Directory.CreateDirectory(storageRootPath);
            }

            Console.WriteLine($"Storage root: {storageRootPath}");
            Console.WriteLine("Server started");


            bool TryResolvePath(string requestPath, out string fullPath)
            {
                fullPath = string.Empty;

                try
                {
                    var trimmed = requestPath.TrimStart('/');
                    var combined = Path.Combine(storageRootPath, trimmed);
                    var normalized = Path.GetFullPath(combined);

                    if (!normalized.StartsWith(storageRootPath))
                        return false;

                    fullPath = normalized;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            IResult InvalidPath() => Results.BadRequest("Invalid path");

            
            app.MapGet("/{**path}", (HttpContext ctx, string path) =>
            {
                Console.WriteLine($"GET {path}");

                if (!TryResolvePath(path, out var fullPath))
                    return InvalidPath();

                // файл
                if (File.Exists(fullPath))
                {
                    return Results.File(fullPath, "application/octet-stream");
                }

                // папка
                if (Directory.Exists(fullPath))
                {
                    var result = new
                    {
                        Path = path,
                        Files = Directory.GetFiles(fullPath).Select(f => new
                        {
                            Name = Path.GetFileName(f),
                            Size = new FileInfo(f).Length,
                            LastModified = File.GetLastWriteTimeUtc(f)
                        }),
                        Directories = Directory.GetDirectories(fullPath).Select(d => new
                        {
                            Name = Path.GetFileName(d),
                            LastModified = Directory.GetLastWriteTimeUtc(d)
                        })
                    };

                    return Results.Ok(result);
                }

                return Results.NotFound();
            });

            app.MapPut("/{**path}", async (HttpContext ctx, string path) =>
            {
                Console.WriteLine($"PUT {path}");

                if (!TryResolvePath(path, out var fullPath))
                    return InvalidPath();

                var dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir!);

                await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                await ctx.Request.Body.CopyToAsync(fs);

                return Results.Created($"/{path}", null);
            });

            app.MapMethods("/{**path}", new[] { "HEAD" }, (HttpContext ctx, string path) =>
            {
                Console.WriteLine($"HEAD {path}");

                if (!TryResolvePath(path, out var fullPath))
                    return InvalidPath();

                if (!File.Exists(fullPath))
                    return Results.NotFound();

                var info = new FileInfo(fullPath);

                ctx.Response.Headers["Content-Length"] = info.Length.ToString();
                ctx.Response.Headers["Last-Modified"] = info.LastWriteTimeUtc.ToString("R");

                return Results.Ok();
            });

            app.MapDelete("/{**path}", (HttpContext ctx, string path) =>
            {
                Console.WriteLine($"DELETE {path}");

                if (!TryResolvePath(path, out var fullPath))
                    return InvalidPath();

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return Results.NoContent();
                }

                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    return Results.NoContent();
                }

                return Results.NotFound();
            });

            app.Run();


        }
    }
}
