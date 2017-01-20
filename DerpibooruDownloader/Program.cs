using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            string title = "DD";
            Console.Title = title;
            string apiKey;
            string domain = "derpibooru.org";
            string cDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string downloadFolder = Get.IsMono() ? "/Downloads" : "\\Downloads";
            cDir = $"{cDir}{downloadFolder}";
            Get.SetDownloadFolder(cDir);
            Directory.CreateDirectory(cDir);
            var consolecolor = Console.ForegroundColor;

            if (Properties.Settings.Default.ApiKey.Length == 0)
            {
                Console.Write("Please enter your API key: ");
                apiKey = Console.ReadLine();
                Properties.Settings.Default.ApiKey = apiKey;
                Properties.Settings.Default.Save();

            }
            else
            {
                apiKey = Properties.Settings.Default.ApiKey;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("This downloads ALL the images that match the tags you specificy.");
            Console.ForegroundColor = consolecolor;
            Console.WriteLine("Check out the search syntax docs to learn how the search should be used.");
            Console.WriteLine("Type 'docs' when asked for tags to be taken to the documentation now.");
            Console.WriteLine();
            Console.Write("Please enter the tags you want to download for: ");
            string tags = Console.ReadLine();
            Console.Write("Finding matching images...");
            if (tags == "docs")
            {
                Process.Start("https://derpibooru.org/search/syntax");
                Environment.Exit(0);
            }
            string requestUrl =
                $"https://{domain}/search.json?q={tags}&key={apiKey}&sf=created_at&sd=desc&perpage=50&page=";
            DerpibooruResponse.Rootobject first_images =
                JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(Get.Derpibooru($"{requestUrl}1").Result);
            List<DerpibooruResponse.Search> allimages = new List<DerpibooruResponse.Search>();
            allimages.AddRange(first_images.search.ToList());
            int total_items = first_images.total;
            int pages = (int) Math.Ceiling(total_items/50.0);

            if (allimages.Count <= total_items)
            {
                for (int i = 2; i <= pages; i++)
                {
                    DerpibooruResponse.Rootobject images =
                        JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(
                            Get.Derpibooru($"{requestUrl}{i}").Result);
                    allimages.AddRange(images.search.ToList());
                }
            }
            Console.WriteLine("Done!");
            Console.WriteLine($"{allimages.Count} images to download!");
            int u = 1;
            foreach (DerpibooruResponse.Search i in allimages)
            {
                Console.Title = $"{title} [{u}/{total_items}]";
                Get.DownloadImage(i.image, i.id);
                u++;
                if (u <= total_items)
                {
                    Console.Title = $"{title} [{u}/{total_items}]";
                }
            }
            Console.WriteLine("DONE ALL!");
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
            if (!File.Exists(downloadPath))
            {
                using (WebClient webConnection = new WebClient())
                {
                    AutoResetEvent notifier = new AutoResetEvent(false);
                    webConnection.DownloadFileCompleted += delegate
                    {
                        Console.WriteLine("Done!");
                        notifier.Set();
                    };

                    webConnection.DownloadFileAsync(new Uri($"https:{url}"), downloadPath);
                    notifier.WaitOne();
                }
            }
            else
            {
                Console.WriteLine("Already exists!");
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