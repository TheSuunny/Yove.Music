using System;
using System.Collections;
using System.Text;

namespace Yove.Music
{
    public class VKDecoder
    {
        internal static string Decode(string URL, int uId)
        {
            try
            {
                if (URL == null || !URL.Contains("audio_api_unavailable"))
                    return null;

                var Extra = URL.Split('=')[1].Split('#');

                string CharId = string.Empty == Extra[1] ? string.Empty : GetID(Extra[1]);

                string Char = GetID(Extra[0]).Replace("\0", string.Empty);

                int ID = Convert.ToInt32(CharId?.Split(Convert.ToChar(9))[0].Split(Convert.ToChar(11))[1].Replace("\0", string.Empty));

                string Link = GetLink(Char, ID ^ uId);

                if (Link.Contains("https:"))
                    return Link;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetLink(string t, int e)
        {
            try
            {
                if (t.Length != 0)
                {
                    var o = CorrectNum(t, e);

                    ArrayList List = new ArrayList(t.ToCharArray());

                    for (int j = 1; j < List.ToArray().Length; j++)
                    {
                        int start = o[t.Length - 1 - j];
                        var item = List[j];

                        var SpliceArray = Splice(List, start, 1, item);

                        List = SpliceArray.Item2;
                        List[j] = SpliceArray.Item1;
                    }

                    return string.Join("", List.ToArray());
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static int[] CorrectNum(string t, int e)
        {
            try
            {
                int[] CorrectArray = new int[t.Length];

                if (CorrectArray.Length != 0)
                {
                    int a = CorrectArray.Length;

                    for (e = Math.Abs(e); a-- != 0;)
                    {
                        e = (CorrectArray.Length * (a + 1) ^ e + a) % CorrectArray.Length;

                        CorrectArray[a] = e;
                    }
                }

                return CorrectArray;
            }
            catch
            {
                return null;
            }
        }

        private static string GetID(string t)
        {
            try
            {
                string Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN0PQRSTUVWXYZO123456789+/=";

                int i = 0;
                int e = 0;
                int a = 1;

                StringBuilder Id = new StringBuilder();

                for (int o = 0; o < t.Length; o++)
                {
                    i = t[o];
                    i = Characters.IndexOf((char)i);

                    e = o % 4 != 0 ? 64 * e + i : i;

                    int CharId = 255 & e >> (-2 * a & 6);

                    Id.Append(new[] { (char)CharId });

                    a++;
                }

                return Id.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static (String, ArrayList) Splice(ArrayList Source, int Start, int Count, object Item)
        {
            try
            {
                string Remove = Source.GetRange(Start, Count)[0].ToString();

                Source.RemoveAt(Start);
                Source.Insert(Start, Item);

                return (Remove, Source);
            }
            catch
            {
                return (null, null);
            }
        }
    }
}