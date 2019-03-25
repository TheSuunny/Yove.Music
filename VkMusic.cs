using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Yove.Http;

namespace Yove.Music
{
    public class VkMusic
    {
        private HttpClient BaseClient = new HttpClient
        {
            EnableProtocolError = false,
            EnableCookies = true,
            EnableAutoRedirect = false,
            Referer = "https://m.vk.com/",
            UserAgent = "Mozilla/5.0 (Linux; U; Android 4.4.2; en-us; SCH-I535 Build/KOT49H) AppleWebKit/534.30 (KHTML, like Gecko) Version/4.0 Mobile Safari/534.30"
        };

        public string Login { get; set; }
        public string Password { get; set; }

        public int uId { get; private set; }

        public bool IsAuth { get; set; }

        public VkMusic() { }

        public VkMusic(string Login, string Password)
        {
            this.Login = Login;
            this.Password = Password;
        }

        public async Task<bool> Auth()
        {
            if (string.IsNullOrEmpty(Login) || string.IsNullOrEmpty(Password))
                throw new ArgumentNullException("Login or Password is null or empty");

            HttpResponse GetURL = await BaseClient.Get("https://m.vk.com/feed");

            HttpResponse GetLogin = await BaseClient.Get(GetURL.Location);

            string LoginURL = HttpUtils.Parser("<form method=\"post\" action=\"", await BaseClient.GetString(GetLogin.Location), "\" novalidate>");

            HttpResponse Auth = await BaseClient.Post(LoginURL, $"email={Login}&pass={Password}", "application/x-www-form-urlencoded");

            HttpResponse GetToken = await BaseClient.Get(Auth.Location);

            if (GetToken.Location != null)
            {
                uId = Convert.ToInt32(HttpUtils.Parser("pid=", await BaseClient.GetString("https://m.vk.com/feed"), ";"));

                return IsAuth = true;
            }

            return false;
        }

        public async Task<int> Count(long Id)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            HttpClient Client = BaseClient;

            string MusicCount = HttpUtils.Parser("class=\"audioPage__count\">", await Client.GetString($"https://m.vk.com/audios{Id}"), " ");

            if (MusicCount != null)
                return Convert.ToInt32(MusicCount);

            return 0;
        }

        public async Task<long> GetUserId(string URL)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            HttpClient Client = BaseClient;

            string UserId = HttpUtils.Parser("<a class=\"pm_item\" href=\"/audios", await Client.GetString($"https://m.vk.com/{URL.Split('/').Last()}"), "\"");

            if (UserId != null)
                return Convert.ToInt64(UserId);

            return 0;
        }

        public async Task<List<Music>> Search(string Query, int Limit = 200)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            if (Limit < 50)
                throw new ArgumentException("Limit min 50");

            List<Music> MusicList = new List<Music>();

            HttpClient Client = BaseClient;

            HttpResponse Search = await Client.Post("https://m.vk.com/audio", $"q={Query.Replace("- ", string.Empty).Replace(" -", string.Empty)}&_ajax=1", "application/x-www-form-urlencoded");

            string MusicUri = $"https://m.vk.com{HttpUtils.Parser("Все аудиозаписи</h3><a class=\"Pad__corner al_empty\" href=\"", Search.Body, "\">")}";

            for (int i = 0; i < Limit; i += 50)
            {
                try
                {
                    if (MusicUri != "https://m.vk.com")
                    {
                        Search = await Client.Post(MusicUri, $"_ajax=1&offset={i}", "application/x-www-form-urlencoded");
                    }
                    else if (MusicList.Count > 0)
                        break;

                    var HowFind = (Search.Body.Contains("AudioSerp__found"))
                        ? Search.Body.Split(new[] { "<div class=\"AudioSerp__found\">" }, StringSplitOptions.None)[1] :
                             Search.Body.Split(new[] { "<div class=\"ArtistPage__search\">" }, StringSplitOptions.None)[1];

                    foreach (string Item in HowFind.Split(new[] { "<div class=\"ai_info\">" }, StringSplitOptions.None))
                    {
                        string Artist = HttpUtils.Parser("<span class=\"ai_artist\">", Item, "</span>").StripHTML();
                        string Title = HttpUtils.Parser("<span class=\"ai_title\">", Item, "</span>").StripHTML();
                        string URL = HttpUtils.Parser("<input type=\"hidden\" value=\"", Item, "\">");
                        string Cover = HttpUtils.Parser("class=\"ai_play\" style=\"background-image:url(", Item, ")");
                        string Duration = HttpUtils.Parser("onclick=\"audioplayer.switchTimeFormat(this, event);\">", Item, "</div>");

                        if (string.IsNullOrEmpty(URL) || string.IsNullOrEmpty(Artist) || string.IsNullOrEmpty(Title) || string.IsNullOrEmpty(Duration))
                            continue;

                        Duration = TimeSpan.Parse($"00:{Duration}").ToString(@"hh\:mm\:ss");
                        URL = VKDecoder.Decode(URL, uId);

                        if (URL == null || URL.Contains("audio_api_unavailable"))
                            continue;

                        Music MusicWrite = new Music
                        {
                            URL = URL,
                            Artist = Artist,
                            Title = Title,
                            Cover = Cover,
                            Duration = Duration
                        };

                        MusicList.Add(MusicWrite);
                    }
                }
                catch { }
            }

            return MusicList.Distinct().ToList();
        }

        public async Task<List<Music>> GetFromUser(string Uri, int Skip = 0)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            HttpClient Client = BaseClient;

            long UserId = await GetUserId(Uri);

            if (UserId == 0)
                throw new ArgumentException("User not found or page close");

            List<Music> MusicList = new List<Music>();

            int MusicCount = await Count(UserId);

            if (MusicCount == 0)
                return MusicList;

            for (int i = (Skip > 50) ? (Skip / 50 * 50 - 50) : 0; i < MusicCount; i += 50)
            {
                try
                {
                    string Search = HttpUtils.Parser("<div class=\"audios_block audios_list _si_container\">", await Client.GetString($"https://m.vk.com/audio?id={UserId}&offset={i}"), "<div class=\"AudioSerp__found\">");

                    if (Search == null)
                        continue;

                    foreach (string Item in Search.Split(new[] { "<div class=\"ai_info\">" }, StringSplitOptions.None))
                    {
                        if (MusicList.Count == MusicCount)
                            break;

                        string Artist = HttpUtils.Parser("<span class=\"ai_artist\">", Item, "</span>").StripHTML();
                        string Title = HttpUtils.Parser("<span class=\"ai_title\">", Item, "</span>").StripHTML();
                        string URL = HttpUtils.Parser("<input type=\"hidden\" value=\"", Item, "\">");
                        string Cover = HttpUtils.Parser("class=\"ai_play\" style=\"background-image:url(", Item, ")");
                        string Duration = HttpUtils.Parser("onclick=\"audioplayer.switchTimeFormat(this, event);\">", Item, "</div>");

                        if (string.IsNullOrEmpty(URL) || string.IsNullOrEmpty(Artist) || string.IsNullOrEmpty(Title) || string.IsNullOrEmpty(Duration))
                            continue;

                        Duration = TimeSpan.Parse($"00:{Duration}").ToString(@"hh\:mm\:ss");
                        URL = VKDecoder.Decode(URL, uId);

                        if (URL == null || URL.Contains("audio_api_unavailable"))
                            continue;

                        Music MusicWrite = new Music
                        {
                            URL = URL,
                            Artist = Artist,
                            Title = Title,
                            Cover = Cover,
                            Duration = Duration
                        };

                        MusicList.Add(MusicWrite);
                    }
                }
                catch { }
            }

            return MusicList.Distinct().ToList();
        }
    }

    public static class Extensions
    {
        public static string StripHTML(this string Input)
        {
            if (string.IsNullOrEmpty(Input))
                return null;

            return WebUtility.HtmlDecode(Regex.Replace(Input, "<.*?>", string.Empty)).Trim();
        }

        public static async Task<string> Save(this Music Item, string Path)
        {
            HttpClient Client = new HttpClient
            {
                EnableProtocolError = false,
                UserAgent = HttpUtils.GenerateUserAgent()
            };

            HttpResponse Request = await Client.Get(Item.URL);

            return Request.ToFile(Path, $@"{Item.Artist.Replace("/", string.Empty)} - {Item.Title.Replace("/", string.Empty)}.mp3");
        }

        public static async Task<Stream> ToStream(this Music Item)
        {
            HttpClient Client = new HttpClient
            {
                EnableProtocolError = false,
                UserAgent = HttpUtils.GenerateUserAgent()
            };

            return await Client.GetStream(Item.URL);
        }

        public static async Task<byte[]> ToBytes(this Music Item)
        {
            HttpClient Client = new HttpClient
            {
                EnableProtocolError = false,
                UserAgent = HttpUtils.GenerateUserAgent()
            };

            return await Client.GetBytes(Item.URL);
        }
    }
}
