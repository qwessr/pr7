using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace HttpNewsPAT
{
    internal class Program
    {
        static Cookie Token;
        static string logFilePath = "debug_trace.log";



        static void Main(string[] args)
        {
            SingIn("emilys", "emilyspass");
            WebRequest request = WebRequest.Create("https://permaviat.ru/");
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Console.WriteLine(response.StatusDescription);
            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();
            Console.WriteLine(responseFromServer);
            reader.Close();
            dataStream.Close();
            response.Close();
            Console.Read();

        }
        public static void ParsingHtml(string htmlCode)
        {
            var html = new HtmlDocument();
            html.LoadHtml(htmlCode);
            var Document = html.DocumentNode;
            IEnumerable DivsNews = Document.Descendants(0).Where(n => n.HasClass("news"));
            foreach (HtmlNode DivNews in DivsNews)
            {
                var src = DivNews.ChildNodes[1].GetAttributeValue("src", "none");
                var name = DivNews.ChildNodes[3].InnerText;
                var description = DivNews.ChildNodes[5].InnerText;
                Console.WriteLine(name + "\n" + "Изображение: " + src + "\n" + "Описание: " + description + "\n");
            }
        }

        public static string GetContent(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(Token);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public static void SingIn(string username, string password)
        {
            CookieContainer cookieContainer = new CookieContainer();
            string url = "";
            Debug.WriteLine($"Выполняем запрос: {url}");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = cookieContainer;

            string postData = $"username={username}&password={password}";
            byte[] data = Encoding.ASCII.GetBytes(postData);
            request.ContentLength = data.Length;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (HttpWebResponse responseCookieGeter = (HttpWebResponse)request.GetResponse())
            {
                Debug.WriteLine($"Статус выполнения: {responseCookieGeter.StatusCode}");
                string cookieHeader = responseCookieGeter.Headers["Set-Cookie"]; ;

                Uri uri = new Uri(url);
                CookieCollection cookies = cookieContainer.GetCookies(uri);

                Console.WriteLine("COOKIES: ");
                foreach (Cookie cookie in cookies)
                {
                    Console.WriteLine($"{cookie.Name}: \n{cookie.Value}");
                }
            }

        }

        public static void WriteToLog(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);

                Debug.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка записи в лог: {ex.Message}");
            }
        }
    }
}
  