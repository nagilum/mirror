using HtmlAgilityPack;
using System.Text;
using System.Text.Json;

namespace mirror
{
    /// <summary>
    /// Application to mirror a given domain locally.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Base URI.
        /// </summary>
        private static Uri? BaseUri { get; set; }

        /// <summary>
        /// Parse command-line arguments.
        /// </summary>
        private static CommandLineArguments CmdArgs { get; set; } = null!;

        /// <summary>
        /// Main download manager.
        /// </summary>
        private static HttpClient DownloadClient { get; } = new();

        /// <summary>
        /// List of errors.
        /// </summary>
        private static List<string> Errors { get; } = new();

        /// <summary>
        /// All scanned URIs.
        /// </summary>
        private static List<Uri> UriQueue { get; } = new();

        /// <summary>
        /// Init all the things..
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        private static async Task Main(string[] args)
        {
            // Log the start.
            var start = DateTimeOffset.Now;

            // Parse the command-line arguments.
            try
            {
                CmdArgs = new CommandLineArguments(args);
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteException(ex);
                return;
            }

            if (CmdArgs?.Uri == null)
            {
                CommandLineArguments.ShowOptions();
                return;
            }

            BaseUri = CmdArgs.Uri;
            UriQueue.Add(CmdArgs.Uri);

            // Set timeout.
            DownloadClient.Timeout = TimeSpan.FromMilliseconds(CmdArgs.TimeoutMilliseconds);

            // Scan the next URI in line.
            var index = -1;

            while (true)
            {
                index++;

                if (index == UriQueue.Count)
                {
                    break;
                }

                await ScanUriAsync(UriQueue[index], index);
            }

            var end = DateTimeOffset.Now;
            var duration = end - start;

            Console.WriteLine();

            // Done scanning the queue, save it to disk.
            try
            {
                var file = Path.Combine(
                    CmdArgs.StoragePath,
                    $"scan-report-{DateTimeOffset.Now:yyyy-MM-dd-HH-mm-ss}.json");

                var obj = new
                {
                    meta = new
                    {
                        start,
                        end,
                        duration
                    },
                    errors = Errors,
                    queue = UriQueue
                };

                var json = JsonSerializer.Serialize(
                    obj,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                ConsoleEx.WriteObjects(
                    "Writing report to ",
                    ConsoleColor.Blue,
                    file,
                    Environment.NewLine,
                    Environment.NewLine,
                    (byte)0x00);

                await File.WriteAllTextAsync(
                    file,
                    json);
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteException(ex);
            }

            // Write some stats.
            ConsoleEx.WriteObjects(
                "Run started ",
                ConsoleColor.Blue,
                start,
                Environment.NewLine,
                (byte) 0x00);

            ConsoleEx.WriteObjects(
                "Run ended ",
                ConsoleColor.Blue,
                end,
                Environment.NewLine,
                (byte) 0x00);

            ConsoleEx.WriteObjects(
                "Run took ",
                ConsoleColor.Blue,
                duration,
                Environment.NewLine,
                (byte) 0x00);

            ConsoleEx.WriteObjects(
                "Total URLs scanned ",
                ConsoleColor.Blue,
                UriQueue.Count,
                Environment.NewLine,
                (byte) 0x00);

            ConsoleEx.WriteObjects(
                "Total errors while scanning: ",
                ConsoleColor.Blue,
                Errors.Count,
                Environment.NewLine,
                (byte) 0x00);
        }

        /// <summary>
        /// Scan a single URI.
        /// </summary>
        /// <param name="uri">Uri to scan.</param>
        /// <param name="index">Index in queue.</param>
        private static async Task ScanUriAsync(Uri uri, int index)
        {
            // Update log.
            ConsoleEx.WriteObjects(
                "[",
                ConsoleColor.Blue,
                index + 1,
                (byte) 0x00,
                "/",
                ConsoleColor.Blue,
                UriQueue.Count,
                (byte) 0x00,
                "] Scanning: ",
                ConsoleColor.Blue,
                uri,
                Environment.NewLine,
                (byte) 0x00);

            // Download the content, or load from local cache.
            var path = GetFilepath(uri);

            byte[]? bytes = null;

            if (File.Exists(path))
            {
                bytes = await LoadFileFromDisk(path);
            }

            if (bytes == null ||
                bytes.Length == 0)
            {
                bytes = await DownloadUriContentAsync(uri);
            }

            if (bytes == null ||
                bytes.Length == 0)
            {
                return;
            }

            // Save a local copy of the file.
            await SaveLocalCopyAsync(
                path,
                bytes);

            // Analyze HTML and extract new links to scan.
            if (CmdArgs.AnalyzeHtmlAndFollowLinks)
            {
                ExtractLinksFromHtml(
                    uri,
                    bytes);
            }
        }

        /// <summary>
        /// Download the content of the URI.
        /// </summary>
        /// <param name="uri">URI to download.</param>
        /// <returns>Content.</returns>
        private static async Task<byte[]?> DownloadUriContentAsync(Uri uri)
        {
            try
            {
                using var response = await DownloadClient.GetAsync(uri);

                var ms = new MemoryStream();
                await response.Content.CopyToAsync(ms);

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Errors.Add($"Error downloading {uri} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Analyze HTML and extract new links to scan.
        /// </summary>
        /// <param name="baseUri">Content source.</param>
        /// <param name="bytes">Content to analyze.</param>
        private static void ExtractLinksFromHtml(Uri baseUri, byte[] bytes)
        {
            var doc = new HtmlDocument();
            HtmlNodeCollection nodes;

            try
            {
                var html = Encoding.UTF8.GetString(bytes);

                doc.LoadHtml(html);
                nodes = doc.DocumentNode.SelectNodes("//a[@href]");

                if (nodes == null)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Errors.Add($"Error parsing HTML: {ex.Message}");
                return;
            }

            foreach (var link in nodes)
            {
                var href = link.GetAttributeValue("href", null);
                
                try
                {
                    if (BaseUri != null)
                    {
                        var uri = new Uri(baseUri, href);

                        if (BaseUri.IsBaseOf(uri) &&
                            !UriQueue.Contains(uri))
                        {
                            UriQueue.Add(uri);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Errors.Add($"Error parsing href: {href} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get the local filepath based on URI.
        /// </summary>
        /// <param name="uri">URI to parse.</param>
        /// <returns>Filepath.</returns>
        private static string GetFilepath(Uri uri)
        {
            var parts = new List<string>
            {
                CmdArgs.StoragePath,
                "local-copies",
                uri.Host
            };

            var isDir = uri.ToString().EndsWith("/");

            foreach (var segment in uri.Segments)
            {
                var temp = segment;

                if (temp.EndsWith("/"))
                {
                    temp = segment.Substring(0, segment.Length - 1);
                }

                if (temp.StartsWith("/"))
                {
                    temp = temp.Substring(1);
                }

                temp = temp
                    .Replace(":", "-")
                    .Replace("/", "-")
                    .Replace("\\", "-")
                    .Replace("%", "-")
                    .Replace("@", "-")
                    .Replace("\t", "-")
                    .Replace("\n", "-")
                    .Replace("\r", "-")
                    .Trim();

                if (string.IsNullOrWhiteSpace(temp))
                {
                    continue;
                }

                parts.Add(temp);
            }

            string filename;

            if (parts.Count > 3 &&
                !isDir)
            {
                filename = parts.Last();
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                filename = "index.html";
            }

            var path = string.Empty;

            foreach (var part in parts)
            {
                path = Path.Combine(
                    path,
                    part);

                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch
                    {
                        //
                    }
                }
            }

            path = Path.Combine(
                path,
                filename);

            return path;
        }

        /// <summary>
        /// Load file from disk.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <returns>Content.</returns>
        private static async Task<byte[]?> LoadFileFromDisk(string path)
        {
            try
            {
                return await File.ReadAllBytesAsync(path);
            }
            catch (Exception ex)
            {
                Errors.Add($"Error reading file: {path} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save the content to a local file.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <param name="bytes">Content to save to disk.</param>
        private static async Task SaveLocalCopyAsync(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                return;
            }

            try
            {
                await File.WriteAllBytesAsync(
                    path,
                    bytes);
            }
            catch (Exception ex)
            {
                Errors.Add($"Error writing local copy: {path} - {ex.Message}");
                return;
            }
        }
    }
}