using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 4) return;

        string mainApp = args[0];
        string downloadUrl = args[1];
        string destinationDir = args[2];
        string excludedFolders = args[3];

        try
        {
            Console.WriteLine("Waiting for the main application to complete...");

            string mainAppName = Path.GetFileNameWithoutExtension(mainApp);
            Process[] processes = Process.GetProcessesByName(mainAppName);

            foreach (var process in processes) process.WaitForExit(10000);

            Thread.Sleep(1000);

            Console.WriteLine("Downloading update...");
            string tempZipPath = Path.Combine(Path.GetTempPath(), "update.zip");

            using (var client = new HttpClient())
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.ConnectionClose = false;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };

                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream
                (
                     tempZipPath,
                     FileMode.Create,
                     FileAccess.Write,
                     FileShare.None,
                     81920,
                     true
                ))
                {
                    await contentStream.CopyToAsync(fileStream, 81920);
                }
            }

            Console.WriteLine("Unpacking update...");

            var excluded = excludedFolders.Split(';');
            string tempExtractDir = Path.Combine(Path.GetTempPath(), "update_extract");

            if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);

            ZipFile.ExtractToDirectory(tempZipPath, tempExtractDir);

            CopyDirectory(tempExtractDir, destinationDir, excluded);

            Console.WriteLine("Launching main application...");
            Process.Start(mainApp);

            File.Delete(tempZipPath);
            Directory.Delete(tempExtractDir, true);

            Console.WriteLine("Update completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.ReadKey();
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir, string[] excludedFolders)
    {
        try
        {
            if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destinationFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destinationFile, true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string directoryName = Path.GetFileName(directory);
                bool shouldExclude = false;

                foreach (string excluded in excludedFolders)
                {
                    if (directoryName.Equals(excluded, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldExclude = true;
                        break;
                    }
                }

                if (!shouldExclude)
                {
                    string destinationSubDir = Path.Combine(destinationDir, directoryName);
                    CopyDirectory(directory, destinationSubDir, excludedFolders);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.ReadKey();
        }
    }
}