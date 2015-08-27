/*
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

        private BlockingCollection<string> _jokes;
        private AutoResetEvent _callbackStopper;

        public Joke(SteamNerd steamNerd) : base(steamNerd)
        {
            _callbackStopper = new AutoResetEvent(false);
            _jokes = new BlockingCollection<string>(QUEUE_SIZE);

            var thread = new Thread(PopulateJokes);
            thread.Start();
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return callback.Message.ToLower() == "!joke";
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            string joke;

            if (_jokes.TryTake(out joke))
            {
                SteamNerd.SendMessage(joke, callback.ChatRoomID, true);
            }
            else
            {
                SteamNerd.SendMessage("Loading jokes...", callback.ChatRoomID, true);
            }
            
        }

        public void PopulateJokes()
        {
            Cef.Initialize(new CefSettings());

            while (true)
            {
                browser = new ChromiumWebBrowser(URL);
                browser.FrameLoadEnd += BrowserFrameLoadEnd;

                _callbackStopper.WaitOne();
            }

            //Cef.Shutdown();
        }

        private async void BrowserFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain)
            {
                browser.FrameLoadEnd -= BrowserFrameLoadEnd;
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


                HtmlDocument jokeDoc;

                try
                {
                    jokeDoc = new HtmlWeb().Load(link);
                }
                catch
                {
                    _callbackStopper.Set();
                    return;
                }
                
                var jokeNode = jokeDoc.DocumentNode.SelectSingleNode("//div[@class='content_wrap']");
                var joke = "";

                foreach (var node in jokeNode.ChildNodes)
                {
                    if (node.Name == "p")
                    {
                        joke += node.OuterHtml + "\n";
                    }
                }

                joke = joke.Replace("<p>", "").Replace("<br>", "\n").Replace("</p>", "");
                _jokes.Add(joke);
                _callbackStopper.Set();
            }
        }
    }
}
*/
