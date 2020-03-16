// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.Runtime;

namespace Microsoft.ML.Internal.Utilities
{
    /// <summary>
    /// This class takes care of downloading resources needed by ML.NET components. Resources are located in
    /// a pre-defined location, that can be overridden by defining Environment variable <see cref="CustomResourcesUrlEnvVariable"/>.
    /// </summary>
    [BestFriend]
    internal sealed class ResourceManagerUtils
    {
        private static volatile ResourceManagerUtils _instance;
        public static ResourceManagerUtils Instance
        {
            get
            {
                return _instance ??
                    Interlocked.CompareExchange(ref _instance, new ResourceManagerUtils(), null) ??
                    _instance;
            }
        }

        private const string DefaultUrl = "https://aka.ms/mlnet-resources/";
        /// <summary>
        /// The location of the resources to download from. Uses either the default location or a location defined
        /// in an Environment variable.
        /// </summary>
        private static string MlNetResourcesUrl
        {
            get
            {
                var envUrl = Environment.GetEnvironmentVariable(CustomResourcesUrlEnvVariable);
                if (!string.IsNullOrEmpty(envUrl))
                    return envUrl;
                return DefaultUrl;
            }
        }

        /// <summary>
        /// An environment variable containing a timeout period (in milliseconds) for downloading resources. If defined,
        /// overrides the timeout defined in the code.
        /// </summary>
        public const string TimeoutEnvVariable = "MICROSOFTML_RESOURCE_TIMEOUT";

        /// <summary>
        /// Environment variable containing optional url to download resources from.
        /// </summary>
        public const string CustomResourcesUrlEnvVariable = "MICROSOFTML_RESOURCE_URL";

        public sealed class ResourceDownloadResults
        {
            public readonly string FileName;
            internal readonly string ErrorMessage;
            internal readonly string DownloadUrl;

            public ResourceDownloadResults(string fileName, string errorMessage, string downloadUrl = null)
            {
                FileName = fileName;
                ErrorMessage = errorMessage;
                DownloadUrl = downloadUrl;
            }
        }

        private ResourceManagerUtils()
        {
        }

        /// <summary>
        /// Generates a url from a suffix.
        /// </summary>
        public static string GetUrl(string suffix)
        {
            return $"{MlNetResourcesUrl}{suffix}";
        }

        /// <summary>
        /// Returns a <see cref="Task"/> that tries to download a resource from a specified url, and returns the path to which it was
        /// downloaded, and an exception if one was thrown.
        /// </summary>
        /// <param name="env">The host environment.</param>
        /// <param name="ch">A channel to provide information about the download.</param>
        /// <param name="relativeUrl">The relative url from which to download.
        /// This is appended to the url defined in <see cref="MlNetResourcesUrl"/>.</param>
        /// <param name="fileName">The name of the file to save.</param>
        /// <param name="dir">The directory where the file should be saved to. The file will be saved in a directory with the specified name inside
        /// a folder called "mlnet-resources" in the <see cref="Environment.SpecialFolder.ApplicationData"/> directory.</param>
        /// <param name="timeout">An integer indicating the number of milliseconds to wait before timing out while downloading a resource.</param>
        /// <returns>The download results, containing the file path where the resources was (or should have been) downloaded to, and an error message
        /// (or null if there was no error).</returns>
        public async Task<ResourceDownloadResults> EnsureResourceAsync(IHostEnvironment env, IChannel ch, string relativeUrl, string fileName, string dir, int timeout)
        {
            var filePath = GetFilePath(ch, fileName, dir, out var error);
            if (File.Exists(filePath) || !string.IsNullOrEmpty(error))
                return new ResourceDownloadResults(filePath, error);

            if (!Uri.TryCreate(Path.Combine(MlNetResourcesUrl, relativeUrl), UriKind.Absolute, out var absoluteUrl))
            {
                return new ResourceDownloadResults(filePath,
                    $"Could not create a valid URI from the base URI '{MlNetResourcesUrl}' and the relative URI '{relativeUrl}'");
            }
            return new ResourceDownloadResults(filePath,
                await DownloadFromUrlWithRetryAsync(env, ch, absoluteUrl.AbsoluteUri, fileName, timeout, filePath), absoluteUrl.AbsoluteUri);
        }

        private async Task<string> DownloadFromUrlWithRetryAsync(IHostEnvironment env, IChannel ch, string url, string fileName, int timeout, string filePath, int retryTimes = 5)
        {
            var downloadResult = "";

            for (int i = 0; i < retryTimes; ++i)
            {
                var thisDownloadResult = await DownloadFromUrlAsync(env, ch, url, fileName, timeout, filePath);

                if (string.IsNullOrEmpty(thisDownloadResult))
                    return thisDownloadResult;
                else
                {
                    downloadResult += thisDownloadResult + @"\n";
                    // Do not retry if the URL does not exist
                    if (thisDownloadResult.Contains("does not exist"))
                        return downloadResult;
                }
                await Task.Delay(10 * 1000);
            }

            return downloadResult;
        }

        /// <returns>Returns the error message if an error occurred, null if download was successful.</returns>
        private async Task<string> DownloadFromUrlAsync(IHostEnvironment env, IChannel ch, string url, string fileName, int timeout, string filePath)
        {
            using (var webClient = new WebClientResponseUri())
            using (var downloadCancel = new CancellationTokenSource())
            {
                bool deleteNeeded = false;
                EventHandler disposed =
                    (object sender, EventArgs e) =>
                    {
                        if (File.Exists(filePath) && deleteNeeded)
                            TryDelete(ch, filePath);
                    };

                webClient.Disposed += disposed;
                var t = Task.Run(() => DownloadResource(env, ch, webClient, new Uri(url), filePath, fileName, downloadCancel.Token));

                UpdateTimeout(ref timeout);
                var timeoutTask = Task.Delay(timeout).ContinueWith(task => default(Exception), TaskScheduler.Default);
                ch.Info($"Downloading {fileName} from {url} to {filePath}");
                var completedTask = await Task.WhenAny(t, timeoutTask);
                if (completedTask != t || completedTask.CompletedResult() != null)
                {
                    downloadCancel.Cancel();
                    deleteNeeded = true;
                    return (await t).Message;
                }
                return null;
            }
        }

        private static void TryDelete(IChannel ch, string filePath, bool warn = true)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception e)
            {
                if (warn)
                    ch.Warning($"File '{filePath}' could not be deleted: {e.Message}");
            }
        }

        private static void UpdateTimeout(ref int timeout)
        {
            var envTimeout = Environment.GetEnvironmentVariable(TimeoutEnvVariable);
            if (!string.IsNullOrWhiteSpace(envTimeout) && int.TryParse(envTimeout, out var res))
                timeout = res;
        }

        /// <summary>
        /// Get the path where the resource should be downloaded to. If the environment variable
        /// is defined, download to the location defined there. Otherwise, download to the "dir" directory
        /// inside <see cref="Environment.SpecialFolder.LocalApplicationData"/>\mlnet-resources\.
        /// </summary>
        private static string GetFilePath(IChannel ch, string fileName, string dir, out string error)
        {
            var envDir = Environment.GetEnvironmentVariable(Utils.CustomSearchDirEnvVariable);
            var appDataBaseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDataDir = Path.Combine(appDataBaseDir, "mlnet-resources");
            var absDir = Path.Combine(string.IsNullOrEmpty(envDir) ? appDataDir : envDir, dir);
            var filePath = Path.Combine(absDir, fileName);
            error = null;

            if (!Directory.Exists(appDataBaseDir))
            {
                try
                {
                    Directory.CreateDirectory(appDataBaseDir);

                    // On unix, create with 0700 perms as per XDG base dir spec
                    if (Environment.OSVersion.Platform == PlatformID.Unix)
                        chmod(appDataBaseDir, 448);
                }
                catch (Exception e)
                {
                    error = $"Error trying to create directory {appDataBaseDir}: {e.Message}.\nPlease fix your " +
                        "filesystem permissions, or try setting the " +
                        $"'{Utils.CustomSearchDirEnvVariable}' environment variable to a writable folder";
                    return filePath;
                }
            }

            // TODO: Also confirm write permission on the directory (maybe use Security.Permissions.FileIOPermission)
            if (!Directory.Exists(absDir))
            {
                try
                {
                    Directory.CreateDirectory(absDir);
                }
                catch (Exception e)
                {
                    error = $"Error trying to create directory {absDir}: {e.Message}.\nPlease try setting the " +
                        $"'{Utils.CustomSearchDirEnvVariable}' environment variable to a writable folder";
                }
            }
            return filePath;
        }

        private Exception DownloadResource(IHostEnvironment env, IChannel ch, WebClientResponseUri webClient, Uri uri, string path, string fileName, CancellationToken ct)
        {
            if (File.Exists(path))
                return null;

            var mutex = new Mutex(false, "Resource" + fileName);
            mutex.WaitOne();
            if (File.Exists(path))
            {
                mutex.ReleaseMutex();
                return null;
            }

            Guid guid = Guid.NewGuid();
            string tempPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "temp-resource-" + guid.ToString()));
            try
            {
                Console.WriteLine(String.Format("1 - Trying to view file through the url: {0}", uri.ToString()));
                using (var s = webClient.OpenRead(uri))
                using (var fh = env.CreateOutputFile(tempPath))
                using (var ws = fh.CreateWriteStream())
                {
                    var headers = webClient.ResponseHeaders.GetValues("Content-Length");
                    var requestUri = webClient.ResponseUri;
                    if (requestUri.Host == "www.microsoft.com" && requestUri.ToString().Length < 60)
                    {
                        return new ArgumentException($"The url '{uri}' does not exist. Url was redirected to '{requestUri}'.");
                    }

                    if (!long.TryParse(headers[0], out var size))
                        size = 10000000;
                    Console.WriteLine(String.Format("2 - Trying to view file through the url: {0}", uri.ToString()));
                    long printFreq = (long)(size / 10.0);
                    var buffer = new byte[4096];
                    long total = 0;
                    int count;
                    // REVIEW: use a progress channel instead.
                    while ((count = s.Read(buffer, 0, 4096)) > 0)
                    {
                        ws.Write(buffer, 0, count);
                        total += count;
                        if ((total - (total / printFreq) * printFreq) <= 4096)
                            ch.Info($"{fileName}: Downloaded {total} bytes out of {size}");
                        if (ct.IsCancellationRequested)
                        {
                            ch.Error($"{fileName}: Download timed out");
                            return ch.Except("Download timed out");
                        }
                    }
                }
                File.Move(tempPath, path);
                ch.Info($"{fileName}: Download complete");
                return null;
            }
            catch (WebException e)
            {
                ch.Error($"{fileName}: Could not download. WebClient returned the following error: {e.Message}");
                return e;
            }
            finally
            {
                TryDelete(ch, tempPath, warn: false);
                mutex.ReleaseMutex();
            }
        }

        public static ResourceDownloadResults GetErrorMessage(out string errorMessage, params ResourceDownloadResults[] result)
        {
            var errorResult = result.FirstOrDefault(res => !string.IsNullOrEmpty(res.ErrorMessage));
            if (errorResult == null)
                errorMessage = null;
            else if (string.IsNullOrEmpty(errorResult.DownloadUrl))
                errorMessage = $"Error downloading resource: {errorResult.ErrorMessage}";
            else
                errorMessage = $"Error downloading resource from '{errorResult.DownloadUrl}': {errorResult.ErrorMessage}";
            return errorResult;
        }

#pragma warning disable IDE1006
        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);
#pragma warning restore IDE1006
    }

    public class WebClientResponseUri : WebClient
    {
        public Uri ResponseUri { get; private set; }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = null;
            response = base.GetWebResponse(request);
            ResponseUri = response.ResponseUri;
            return response;
        }
    }
}
