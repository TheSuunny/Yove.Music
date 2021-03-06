﻿using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Yove.Http;
using Yove.Http.Proxy;

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

        public ProxyClient Proxy
        {
            get
            {
                return BaseClient.Proxy;
            }
            set
            {
                if (value != null)
                    BaseClient.Proxy = value;
            }
        }

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

            HttpResponse GetURL = await BaseClient.Get("https://m.vk.com/feed").ConfigureAwait(false);

            HttpResponse GetLogin = await BaseClient.Get(GetURL.Location).ConfigureAwait(false);

            string LoginURL = HttpUtils.Parser("<form method=\"post\" action=\"", await BaseClient.GetString(GetLogin.Location).ConfigureAwait(false), "\" novalidate>");

            HttpResponse Auth = await BaseClient.Post(LoginURL, $"email={Login}&pass={Password}", "application/x-www-form-urlencoded").ConfigureAwait(false);

            HttpResponse GetToken = await BaseClient.Get(Auth.Location).ConfigureAwait(false);

            if (GetToken.Location != null)
            {
                uId = Convert.ToInt32(HttpUtils.Parser("pid=", await BaseClient.GetString("https://m.vk.com/feed").ConfigureAwait(false), ";"));

                return IsAuth = true;
            }

            return false;
        }

        public async Task<int> Count(long Id)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            using (HttpClient Client = (HttpClient)BaseClient.Clone())
            {
                string MusicCount = HttpUtils.Parser("class=\"audioPage__count\">", await Client.GetString($"https://m.vk.com/audios{Id}").ConfigureAwait(false), " ");

                if (MusicCount != null)
                    return Convert.ToInt32(MusicCount);

                return 0;
            }
        }

        public async Task<long> GetUserId(string URL)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            using (HttpClient Client = (HttpClient)BaseClient.Clone())
            {
                string UserId = HttpUtils.Parser("<a class=\"pm_item\" href=\"/audios", await Client.GetString($"https://m.vk.com/{URL.Split('/').Last()}").ConfigureAwait(false), "\"");

                if (UserId != null)
                    return Convert.ToInt64(UserId);

                return 0;
            }
        }

        public async Task<List<Music>> Search(string Query, int Limit = 200)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            if (Limit < 50)
                throw new ArgumentException("Limit min 50");

            List<Music> MusicList = new List<Music>();

            using (HttpClient Client = (HttpClient)BaseClient.Clone())
            {
                HttpResponse Search = await Client.Post("https://m.vk.com/audio", $"q={Query.Replace("- ", string.Empty).Replace(" -", string.Empty)}&_ajax=1", "application/x-www-form-urlencoded").ConfigureAwait(false);

                string MusicUri = $"https://m.vk.com{HttpUtils.Parser("Все аудиозаписи</h3><a class=\"Pad__corner al_empty\" href=\"", Search.Body, "\">")}";

                for (int i = 0; i < Limit; i += 50)
                {
                    try
                    {
                        if (MusicUri != "https://m.vk.com")
                        {
                            Search = await Client.Post(MusicUri, $"_ajax=1&offset={i}", "application/x-www-form-urlencoded").ConfigureAwait(false);
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
        }

        public async Task<List<Music>> GetFromUser(string Uri, int Skip = 0)
        {
            if (!IsAuth)
                throw new Exception("Not authorization");

            using (HttpClient Client = (HttpClient)BaseClient.Clone())
            {
                long UserId = await GetUserId(Uri).ConfigureAwait(false);

                if (UserId == 0)
                    throw new ArgumentException("User not found or page close");

                List<Music> MusicList = new List<Music>();

                int MusicCount = await Count(UserId).ConfigureAwait(false);

                if (MusicCount == 0)
                    return MusicList;

                for (int i = 0; i < MusicCount; i += 50)
                {
                    try
                    {
                        if (Skip != 0 && MusicList.Count >= MusicCount - (Skip / 50 * 50))
                            break;

                        string Search = HttpUtils.Parser("<div class=\"audios_block audios_list _si_container\">", await Client.GetString($"https://m.vk.com/audio?id={UserId}&offset={i}").ConfigureAwait(false), "<div class=\"AudioSerp__found\">");

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
            using (HttpClient Client = new HttpClient
            {
                EnableProtocolError = false,
                UserAgent = HttpUtils.GenerateUserAgent()
            })
            {
                HttpResponse Request = await Client.Get(Item.URL).ConfigureAwait(false);

                return await Request.ToFile(Path, $@"{Item.Artist.Replace("/", string.Empty)} - {Item.Title.Replace("/", string.Empty)}.mp3").ConfigureAwait(false);
            }
        }

        public static async Task<Stream> ToStream(this Music Item)
        {
            using (HttpClient Client = new HttpClient
            {
                EnableProtocolError = false,
                UserAgent = HttpUtils.GenerateUserAgent()
            })
            {
                return await Client.GetStream(Item.URL).ConfigureAwait(false);
            }
        }

        public static async Task<byte[]> ToBytes(this Music Item)
        {
            using (HttpClient Client = new HttpClient
            {
                EnableProtocolError = false,
                UserAgent = HttpUtils.GenerateUserAgent()
            })
            {
                return await Client.GetBytes(Item.URL).ConfigureAwait(false);
            }
        }
    }
}
