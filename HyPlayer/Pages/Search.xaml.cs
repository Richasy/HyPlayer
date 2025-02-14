﻿using HyPlayer.Classes;
using HyPlayer.Controls;
using NeteaseCloudMusicApi;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Windows.System;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace HyPlayer.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class Search : Page
    {
        private int page = 0;
        private string Text = "";
        public Search()
        {
            InitializeComponent();
            NavigationViewSelector.SelectedItem = NavigationViewSelector.MenuItems[0];



            this.NavigationCacheMode = NavigationCacheMode.Required;

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var list = HistoryManagement.GetSearchHistory();
            foreach (string item in list)
            {
                var btn = new Button()
                {
                    Content = item
                };
                btn.Click += Btn_Click;
                SearchHistory.Children.Add(btn);
            }
            if (e.Parameter != null)
            {
                SearchKeywordBox.Text = e.Parameter.ToString();
                Text = SearchKeywordBox.Text;
                LoadResult();
            }

        }
        private async void LoadResult()
        {
            if (string.IsNullOrEmpty(Text)) return;
            if (Convert.ToBase64String(Text.ToByteArrayUtf8()) == "6Ieq5p2A")
            {
                _ = Launcher.LaunchUriAsync(new Uri(@"http://music.163.com/m/topic/18926801"));
                return;
            }
            HistoryManagement.AddSearchHistory(Text);
            var list = HistoryManagement.GetSearchHistory();
            SearchHistory.Children.Clear();
            foreach (string item in list)
            {
                var btn = new Button()
                {
                    Content = item
                };
                btn.Click += Btn_Click;
                SearchHistory.Children.Add(btn);
            }
            SearchResultContainer.Children.Clear();
            var (isOk, json) = await Common.ncapi.RequestAsync(CloudMusicApiProviders.Cloudsearch,
                new Dictionary<string, object>()
                {
                    {"keywords", Text},
                    {"type", ((NavigationViewItem) NavigationViewSelector.SelectedItem).Tag.ToString()},
                    { "offset", page * 30 }
                });
            if (isOk)
            {
                switch (((NavigationViewItem)NavigationViewSelector.SelectedItem).Tag.ToString())
                {
                    case "1":
                        LoadSongResult(json);
                        break;
                    case "10":
                        LoadAlbumResult(json);
                        break;
                    case "100":
                        LoadArtistResult(json);
                        break;
                    case "1000":
                        LoadPlaylistResult(json);
                        break;
                }
            }
        }

        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            SearchKeywordBox.Text = ((Button)sender).Content.ToString();
            Text = SearchKeywordBox.Text;
            LoadResult();
        }

        private void LoadPlaylistResult(JObject json)
        {
            foreach (var pljs in json["result"]["playlists"].ToArray())
            {
                SearchResultContainer.Children.Add(new SinglePlaylistStack(NCPlayList.CreateFromJson(pljs)));
            }
            if (int.Parse(json["result"]["playlistCount"].ToString()) >= (page + 1) * 30)
            {
                NextPage.Visibility = Visibility.Visible;
            }
            else
            {
                NextPage.Visibility = Visibility.Collapsed;
            }
            if (page > 0)
            {
                PrevPage.Visibility = Visibility.Visible;
            }
            else
            {
                PrevPage.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadArtistResult(JObject json)
        {
            foreach (var singerjson in json["result"]["artists"].ToArray())
            {
                SearchResultContainer.Children.Add(new SingleArtist(NCArtist.CreateFormJson(singerjson)));
            }
            if (int.Parse(json["result"]["artistCount"].ToString()) >= (page + 1) * 30)
            {
                NextPage.Visibility = Visibility.Visible;
            }
            else
            {
                NextPage.Visibility = Visibility.Collapsed;
            }
            if (page > 0)
            {
                PrevPage.Visibility = Visibility.Visible;
            }
            else
            {
                PrevPage.Visibility = Visibility.Collapsed;
            }

        }

        private void LoadAlbumResult(JObject json)
        {
            foreach (var albumjson in json["result"]["albums"].ToArray())
            {
                SearchResultContainer.Children.Add(new SingleAlbum(NCAlbum.CreateFormJson(albumjson), albumjson["artists"].ToArray().Select(t => NCArtist.CreateFormJson(t)).ToList()));
            }
            if (int.Parse(json["result"]["albumCount"].ToString()) >= (page + 1) * 30)
            {
                NextPage.Visibility = Visibility.Visible;
            }
            else
            {
                NextPage.Visibility = Visibility.Collapsed;
            }
            if (page > 0)
            {
                PrevPage.Visibility = Visibility.Visible;
            }
            else
            {
                PrevPage.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadSongResult(JObject json)
        {
            int idx = 0;
            foreach (JToken song in json["result"]["songs"].ToArray())
            {
                NCSong NCSong = NCSong.CreateFromJson(song);
                SearchResultContainer.Children.Add(new SingleNCSong(NCSong, idx++, song["privilege"]["st"].ToString() == "0"));
            }

            if (int.Parse(json["result"]["songCount"].ToString()) >= (page + 1) * 30)
            {
                NextPage.Visibility = Visibility.Visible;
            }
            else
            {
                NextPage.Visibility = Visibility.Collapsed;
            }
            if (page > 0)
            {
                PrevPage.Visibility = Visibility.Visible;
            }
            else
            {
                PrevPage.Visibility = Visibility.Collapsed;
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            Text = sender.Text;
            LoadResult();
        }

        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(sender.Text))
            {
                AutoSuggestBox_GotFocus(sender, null);
                return;
            }

            (bool isOk, JObject json) = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SearchSuggest,
                new Dictionary<string, object>() { { "keywords", sender.Text }, { "type", "mobile" } });

            if (isOk && json["result"] != null && json["result"]["allMatch"] != null && json["result"]["allMatch"].HasValues)
            {
                sender.ItemsSource = json["result"]["allMatch"].ToArray().ToList().Select(t => t["keyword"].ToString())
                    .ToList();
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender,
            AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString();
        }


        private async void AutoSuggestBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace((sender as AutoSuggestBox)?.Text))
            {
                (bool isOk, JObject json) = await Common.ncapi.RequestAsync(CloudMusicApiProviders.SearchHot);
                if (isOk)
                {
                    ((AutoSuggestBox)sender).ItemsSource =
                        json["result"]["hots"].ToArray().ToList().Select(t => t["first"].ToString());
                }
            }
        }
        private void PrevPage_OnClick(object sender, RoutedEventArgs e)
        {
            page--;
            LoadResult();
        }

        private void NextPage_OnClickPage_OnClick(object sender, RoutedEventArgs e)
        {
            page++;
            LoadResult();
        }

        private void NavigationView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            page = 0;
            LoadResult();
        }

        private void AutoSuggestBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ((AutoSuggestBox)sender).ItemsSource = null;
        }
    }
}
