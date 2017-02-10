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
using static System.Console;

namespace DerpibooruDownloader
{
   class Program
    {
        static void Main()
        {
            string title = "DD 0.4";
            Title = title;
            string requestUrl = string.Empty;
            List<string> commands = new List<string> {"docs", "clear"};
            string apiKey = string.Empty;
            string filter = string.Empty;
            string domain = "derpibooru.org";
            string cDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string downloadFolder = Get.IsMono() ? "/Downloads" : "\\Downloads";
            cDir = $"{cDir}{downloadFolder}";
            Get.SetDownloadFolder(cDir);
            Directory.CreateDirectory(cDir);
            if (Properties.Settings.Default.ApiKey.Length <= 0)
            {
                Write("Do you want to use an API key? Y/N ");
                string s = ReadKey().KeyChar.ToString().ToLower();
                if (s == "y")
                {
                    WriteLine();
                    Write("Please enter your API key: ");
                    apiKey = ReadLine();
                    Properties.Settings.Default.ApiKey = apiKey;
                    Properties.Settings.Default.Save();
                    apiKey = "&key=" + apiKey;
                }
                else
                {
                    Properties.Settings.Default.ApiKey = "none";
                    Properties.Settings.Default.Save();
                    WriteLine();
                }
            }
            else
            {
                if (Properties.Settings.Default.ApiKey != "none")
                {
                    WriteLine("API Key found!");
                    apiKey = "&key=" + Properties.Settings.Default.ApiKey;
                }
            }

            if (Properties.Settings.Default.FilterId.Length <= 0)
            {
                Write("Do you want to use an Filter ID key? Y/N ");
                string s = ReadKey().KeyChar.ToString().ToLower();
                if (s == "y")
                {
                    WriteLine();
                    Write("Please enter your Filter ID key: ");
                    filter = ReadLine();
                    Properties.Settings.Default.FilterId = filter;
                    Properties.Settings.Default.Save();
                    filter = "&filter_id=" + filter;
                }
                else
                {
                    Properties.Settings.Default.FilterId = "none";
                    Properties.Settings.Default.Save();
                    WriteLine();
                }
            }
            else
            {
                if (Properties.Settings.Default.FilterId != "none")
                {
                    WriteLine("Filter ID found!");
                    filter = "&filter_id=" + Properties.Settings.Default.FilterId;
                }
            }

            WriteLine("Check out the search syntax docs to learn how the search should be used.");
            WriteLine("You can also enter the following when asked for tags:");
            WriteLine("'docs'  - page with search documentation will open.");
            WriteLine("'clear' - delete the stored API key and filter ID.");
            WriteLine();
            Write("Please enter the tags you want to download for: ");
            string tags = ReadLine();
            if (!commands.Contains(tags))
            {
                requestUrl =
                    $"https://{domain}/search.json?q={tags}{apiKey}{filter}&sf=created_at&sd=desc&perpage=50&page=";
                int count =
                    JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(Get.Derpibooru($"{requestUrl}1").Result)
                        .total;
                WriteLine($"{count} matching images found!");
            }
            else
            {
                RunCommand(tags);
            }
            Write("How many images do you want to download? (0 = ALL): ");
            string input = ReadLine();
            int limit = 0;
            if (input != null)
            {
                try
                {
                    limit = int.Parse(input);
                }
                catch (Exception)
                {
                    limit = 0;
                }
              
            }
          
            Write("Finding matching images...");
            

            DerpibooruResponse.Rootobject firstImages =
                JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(Get.Derpibooru($"{requestUrl}1").Result);
            List<DerpibooruResponse.Search> allimages = new List<DerpibooruResponse.Search>();
            allimages.AddRange(firstImages.search.ToList());
            int totalItems = firstImages.total;
            int pagesToGet;
            if (limit != 0)
            {
                pagesToGet = (int) Math.Ceiling(limit/50.0);
            }
            else
            {
                pagesToGet = (int) Math.Ceiling(totalItems/50.0);
            }

            while (allimages.Count < pagesToGet * 50 && (totalItems > allimages.Count))
            {
                for (int i = 2; i <= pagesToGet; i++)
                {
                    DerpibooruResponse.Rootobject images =
                        JsonConvert.DeserializeObject<DerpibooruResponse.Rootobject>(Get.Derpibooru($"{requestUrl}{i}").Result);
                    allimages.AddRange(images.search.ToList());
                }            
            }
            int titlenum = allimages.Count;
            if ((titlenum > limit ) && limit != 0) 
            { 
                int l = allimages.Count - limit;
                allimages.RemoveRange(limit -1 , l);
                titlenum = allimages.Count;
            }
            
            WriteLine("Done!");
            WriteLine($"{titlenum} images to download!");
            WriteLine("Press ANY button to start downloading.");
            ReadLine();
            int u = 1;
            if (limit == 0) limit = titlenum;
            foreach (DerpibooruResponse.Search i in allimages)
            {
                Title = $"{title} [{u}/{limit}]";
                Get.DownloadImage(i.image, i.id);
                u++;
                if (u <= limit)
                {
                    Title = $"{title} [{u}/{limit}]";
                }
            }
            Process.Start(cDir);
            WriteLine("DONE ALL!");
            ReadLine();
        }

        private static void RunCommand(string tags)
        {
            switch (tags)
            {
                case "docs":
                    Process.Start("https://derpibooru.org/search/syntax");
                    Environment.Exit(0);
                    break;
                case "clear":
                    Write("Cleared config, restart the application.");
                    Properties.Settings.Default.FilterId = null;
                    Properties.Settings.Default.ApiKey = null;
                    Properties.Settings.Default.Save();
                    ReadLine();
                    Environment.Exit(0);
                    break;
            }
        }
    }

    public class Get
    {
        private static string _cDir;

        public static void DownloadImage(string url, string id)
        {
            Write($"Downloading {id}...");
            string extension = Path.GetExtension(url);
            string fileName = $"{id}{extension}";

            string downloadPath = IsMono() ? $@"{_cDir}/{fileName}" : $@"{_cDir}\{fileName}";
            if (!File.Exists(downloadPath))
            {
                using (WebClient webConnection = new WebClient())
                {
                    AutoResetEvent notifier = new AutoResetEvent(false);
                    webConnection.DownloadFileCompleted += delegate
                    {
                        WriteLine("Done!");
                        notifier.Set();
                    };

                    webConnection.DownloadFileAsync(new Uri($"https:{url}"), downloadPath);
                    notifier.WaitOne();
                }
            }
            else
            {
                WriteLine("Already exists!");
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
            _cDir = c;
        }
    }
}