using System;
using CefSharp;
using CefSharp.OffScreen;
using SteamKit2;
using HtmlAgilityPack;

namespace STEAMNERD.Modules
{
    class Joke : Module
    {
        private const int QUEUE_SIZE = 10;
        private const string URL = "http://jokes.cc.com/";
        private ChromiumWebBrowser browser;

        public Joke(SteamNerd steamNerd) : base(steamNerd)
        {
            Cef.Initialize(new CefSettings());

            browser = new ChromiumWebBrowser(URL);
            browser.FrameLoadEnd += BrowserFrameLoadEnd;

            Console.Read();
            Cef.Shutdown();
        }

        private async void BrowserFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain)
            {
                browser.FrameLoadEnd -= BrowserFrameLoadEnd;

                Console.WriteLine("Found main frame.");
                var source = await browser.GetSourceAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(source);
                var randomJoke = doc.GetElementbyId("random_joke").ChildNodes;
                var link = "";

                foreach (var node in randomJoke)
                {
                    if (node.Name == "a")
                    {
                        link = node.Attributes["href"].Value;
                        break;
                    }
                }

                var jokeDoc = new HtmlWeb().Load(link);
                var jokeNode = jokeDoc.DocumentNode.SelectSingleNode("//div[@class='content_wrap']");
                var joke = "";

                foreach (var node in jokeNode.ChildNodes)
                {
                    if (node.Name == "p")
                    {
                        var text = node.InnerText.Trim();
                        if (text == "") continue;

                        joke += text + "\n";
                    }
                }

                Console.WriteLine(joke);
            }
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            throw new NotImplementedException();
        }
    }
}
