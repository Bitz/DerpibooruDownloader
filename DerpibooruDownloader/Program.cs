using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DerpibooruDownloader
{
    class Program
    {
        static void Main()
        {
            string tags = "artist:atryl, safe, twilight sparkle, fluttershy";
            string apiKey = "";
            string domain = "derpibooru.org";
            string cDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string downloadFolder = Get.IsMono() ? "/Downloads" : "\\Downloads";
            cDir = $"{cDir}{downloadFolder}";
            Get.SetDownloadFolder(cDir);
            Directory.CreateDirectory(cDir);
            string requestUrl =
                $"https://{domain}/search.json?q={tags}&key={apiKey}&sf=created_at&sd=desc&perpage=50&page=";
            DerpibooruResponse.Rootobject first_images =
                JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(Get.Derpibooru($"{requestUrl}1").Result);
            List<DerpibooruResponse.Search> allimages = new List<DerpibooruResponse.Search>();
            allimages.AddRange(first_images.search.ToList());
            int total_items = first_images.total;
            int pages = (int) Math.Ceiling(total_items/50.0);
            if (allimages.Count != total_items)
            {
                for (int i = 2; i <= pages; i++)
                {
                    DerpibooruResponse.Rootobject images =
                        JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(
                            Get.Derpibooru($"{requestUrl}{i}").Result);
                    allimages.AddRange(images.search.ToList());
                }
            }

            Console.WriteLine($"{allimages.Count} images to download!");

            foreach (DerpibooruResponse.Search i in allimages)
            {
                Get.DownloadImage(i.image, i.id);
                Console.WriteLine("DONE!");
            }
            Console.WriteLine("DONE!");
            Console.ReadLine();
        }
    }

    public class Get
    {
        private static string cDir;

        public static void DownloadImage(string url, string id)
        {
            Console.Write($"Downloading {id}...");
            string extension = Path.GetExtension(url);
            string fileName = $"{id}{extension}";
            string downloadPath = IsMono() ? $@"{cDir}/{fileName}" : $@"{cDir}\{fileName}";

            using (WebClient webConnection = new WebClient())
            {
                var notifier = new AutoResetEvent(false);
                long lReceived;
                long lTotal;
                webConnection.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
                {
                    Console.Clear();
                    Console.WriteLine(e.ProgressPercentage + "% (" + e.BytesReceived/1024f + "kb of " +
                                      e.TotalBytesToReceive/1024f + "kb)");
                    lReceived = e.BytesReceived;
                    lTotal = e.TotalBytesToReceive;

                    // Indicate that things are done
                    if (lReceived >= lTotal) notifier.Set();
                };

                webConnection.DownloadFileAsync(new Uri($"https:{url}"), downloadPath);
                notifier.WaitOne();
            }
        }

        public static async Task<string> Derpibooru(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                string type = "application/json";
                client.BaseAddress = new Uri(url);

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(type));
                HttpResponseMessage response = await client.GetAsync(String.Empty);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                return string.Empty;
            }
        }

        public static bool IsMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        public static void SetDownloadFolder(string c)
        {
            cDir = c;
        }
    }
}