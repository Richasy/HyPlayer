﻿using HyPlayer.Classes;
using HyPlayer.HyPlayControl;
using HyPlayer.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Toolkit.Uwp.Notifications;
using NeteaseCloudMusicApi;
using Windows.Media;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace HyPlayer.Controls
{
    public sealed partial class PlayBar : UserControl
    {
        private bool canslide;
        public TimeSpan nowtime => HyPlayList.Player.PlaybackSession.Position;

        public PlayBar()
        {
            Common.BarPlayBar = this;
            InitializeComponent();
            HyPlayList.Player.Volume = (double)Common.Setting.Volume / 100;
            SliderAudioRate.Value = HyPlayList.Player.Volume * 100;
            HyPlayList.OnPlayItemChange += LoadPlayingFile;
            HyPlayList.OnPlayPositionChange += OnPlayPositionChange;
            HyPlayList.OnPlayPositionChange += UpdateMSTC;
            HyPlayList.OnPlayListAddDone += HyPlayList_OnPlayListAdd;
            AlbumImage.Source = new BitmapImage(new Uri("ms-appx:Assets/icon.png"));
            InitializeDesktopLyric();
        }

        private void UpdateMSTC(TimeSpan pos)
        {
            // Create our timeline properties object 
            var timelineProperties = new SystemMediaTransportControlsTimelineProperties();

            // Fill in the data, using the media elements properties 
            timelineProperties.StartTime = TimeSpan.FromSeconds(0);
            timelineProperties.MinSeekTime = TimeSpan.FromSeconds(0);
            timelineProperties.Position = HyPlayList.Player.PlaybackSession.Position;
            timelineProperties.MaxSeekTime =
                TimeSpan.FromMilliseconds(HyPlayList.NowPlayingItem.AudioInfo.LengthInMilliseconds);
            timelineProperties.EndTime =
                TimeSpan.FromMilliseconds(HyPlayList.NowPlayingItem.AudioInfo.LengthInMilliseconds);
            // Update the System Media transport Controls 
            HyPlayList.MediaSystemControls.UpdateTimelineProperties(timelineProperties);
        }

        public void InitializeDesktopLyric()
        {
            if (Common.Setting.toastLyric)
            {
                ToastContentBuilder desktopLyricsToast = new ToastContentBuilder();
                desktopLyricsToast.SetToastScenario(ToastScenario.IncomingCall);
                desktopLyricsToast.AddAudio(new ToastAudio() { Silent = true });
                desktopLyricsToast.AddVisualChild(new AdaptiveText()
                {
                    Text = new BindableString("Title"),
                    HintStyle = AdaptiveTextStyle.Header
                });
                desktopLyricsToast.AddVisualChild(new AdaptiveText()
                {
                    Text = new BindableString("PureLyric"),
                });
                desktopLyricsToast.AddVisualChild(new AdaptiveText()
                {
                    Text = new BindableString("Translation"),
                });
                desktopLyricsToast.AddVisualChild(new AdaptiveProgressBar()
                {
                    ValueStringOverride = new BindableString("TotalValueString"),

                    Status = new BindableString("CurrentValueString"),

                    Value = new BindableProgressBarValue("CurrentValue"),
                });
                var toast = new ToastNotification(desktopLyricsToast.GetXml())
                {
                    Tag = "HyPlayerDesktopLyrics",
                    Data = new NotificationData()
                };
                toast.Data.Values["Title"] = "当前无音乐播放";
                toast.Data.Values["PureLyric"] = "当前无歌词";
                toast.Data.Values["TotalValueString"] = "0:00:00";
                toast.Data.Values["CurrentValueString"] = "0:00:00";
                toast.Data.Values["CurrentValue"] = "0";

                toast.Data.SequenceNumber = 0;
                toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(60);
                ToastNotifier notifier = ToastNotificationManager.CreateToastNotifier();
                notifier.Show(toast);
                toast.Dismissed += Toast_Dismissed;
                HyPlayList.OnPlayPositionChange += FreshDesktopLyric;
            }
            else
            {
                HyPlayList.OnPlayPositionChange -= FreshDesktopLyric;
                ToastNotifier notifier = ToastNotificationManager.CreateToastNotifier();
                ToastNotificationManagerCompat.History.Clear();
            }
        }

        private void Toast_Dismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            if (args.Reason == ToastDismissalReason.TimedOut)
            {
                ToastNotifier notifier = ToastNotificationManager.CreateToastNotifier();
                notifier.Show(sender);
            }
            else if (Common.Setting.toastLyric)
                Common.Setting.toastLyric = false;
        }

        private void FreshDesktopLyric(TimeSpan ts)
        {
            AudioInfo tai = HyPlayList.NowPlayingItem.AudioInfo;
            NotificationData data = new NotificationData()
            {
                SequenceNumber = 0
            };
            data.Values["Title"] = HyPlayList.NowPlayingItem.Name;
            data.Values["PureLyric"] = HyPlayList.Lyrics[HyPlayList.lyricpos].PureLyric;
            data.Values["Translation"] = HyPlayList.Lyrics[HyPlayList.lyricpos].Translation is null
                ? " "
                : HyPlayList.Lyrics[HyPlayList.lyricpos].Translation;
            data.Values["TotalValueString"] =
                TimeSpan.FromMilliseconds(tai.LengthInMilliseconds).ToString(@"hh\:mm\:ss");
            data.Values["CurrentValueString"] = HyPlayList.Player.PlaybackSession.Position.ToString(@"hh\:mm\:ss");
            data.Values["CurrentValue"] = (HyPlayList.Player.PlaybackSession.Position /
                                           TimeSpan.FromMilliseconds(tai.LengthInMilliseconds)).ToString();
            NotificationUpdateResult res = ToastNotificationManager.CreateToastNotifier()
                .Update(data, "HyPlayerDesktopLyrics");
        }

        private void HyPlayList_OnPlayListAdd()
        {
            RefreshSongList();
        }

        private async void TestFile()
        {
            FileOpenPicker fop = new FileOpenPicker();
            fop.FileTypeFilter.Add(".flac");
            fop.FileTypeFilter.Add(".mp3");


            System.Collections.Generic.IReadOnlyList<Windows.Storage.StorageFile> files =
                await fop.PickMultipleFilesAsync();
            HyPlayList.RemoveAllSong();
            foreach (Windows.Storage.StorageFile file in files)
            {
                await HyPlayList.AppendFile(file);
            }

            HyPlayList.SongAppendDone();
            HyPlayList.SongMoveTo(0);
        }

        public void OnPlayPositionChange(TimeSpan ts)
        {
            Common.Invoke(() =>
            {
                try
                {
                    if (HyPlayList.NowPlayingItem == null) return;
                    AudioInfo tai = HyPlayList.NowPlayingItem.AudioInfo;
                    TbSingerName.Text = tai.Artist;
                    TbSongName.Text = tai.SongName;
                    canslide = false;
                    SliderProgress.Value = HyPlayList.Player.PlaybackSession.Position.TotalMilliseconds;
                    canslide = true;
                    TextBlockTotalTime.Text =
                        TimeSpan.FromMilliseconds(tai.LengthInMilliseconds).ToString(@"hh\:mm\:ss");
                    TextBlockNowTime.Text =
                        HyPlayList.Player.PlaybackSession.Position.ToString(@"hh\:mm\:ss");
                    PlayStateIcon.Glyph =
                        HyPlayList.Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing
                            ? "\uEDB4"
                            : "\uEDB5";
                    //SliderAudioRate.Value = mp.Volume;
                }
                catch
                {
                    //ignore
                }
            });
        }

        public void LoadPlayingFile(HyPlayItem mpi)
        {
            if (Common.GLOBAL["PERSONALFM"].ToString() == "true")
            {
                IconPrevious.Glyph = "\uE7E8";
                IconPlayType.Glyph = "\uE107";
            }
            else
            {
                IconPrevious.Glyph = "\uE892";
                switch (HyPlayList.NowPlayType)
                {
                    case PlayMode.Shuffled:
                        //随机
                        IconPlayType.Glyph = "\uE14B";
                        break;
                    case PlayMode.SinglePlay:
                        //单曲
                        IconPlayType.Glyph = "\uE1CC";
                        break;
                    case PlayMode.DefaultRoll:
                        //顺序
                        IconPlayType.Glyph = "\uE169";
                        break;
                }
            }

            if (mpi == null)
            {
                return;
            }

            AudioInfo ai = mpi.AudioInfo;
            Common.Invoke((async () =>
            {
                TbSingerName.Text = ai.Artist;
                TbSongName.Text = ai.SongName;
                if (mpi.ItemType == HyPlayItemType.Local)
                {
                    BitmapImage img = new BitmapImage();
                    await img.SetSourceAsync(
                        (await mpi.AudioInfo.LocalSongFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 9999)));
                    AlbumImage.Source = img;
                }
                else
                {
                    AlbumImage.Source =
                        new BitmapImage(new Uri(mpi.AudioInfo.Picture + "?param=" +
                                                StaticSource.PICSIZE_PLAYBAR_ALBUMCOVER));
                }

                SliderAudioRate.Value = HyPlayList.Player.Volume * 100;
                SliderProgress.Minimum = 0;
                SliderProgress.Maximum = ai.LengthInMilliseconds;
                if (mpi.ItemType == HyPlayItemType.Netease)
                {
                    BtnLike.IsChecked = Common.LikedSongs.Contains(mpi.NcPlayItem.id);
                    HistoryManagement.AddNCSongHistory(mpi.NcPlayItem.id);
                }

                ListBoxPlayList.SelectedIndex = HyPlayList.NowPlaying;
                TbSongTag.Text = HyPlayList.NowPlayingItem.AudioInfo.tag;
                Btn_Share.IsEnabled = HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.Netease;
            }));
        }

        public void RefreshSongList()
        {
            try
            {
                ObservableCollection<ListViewPlayItem> Contacts = new ObservableCollection<ListViewPlayItem>();
                for (int i = 0; i < HyPlayList.List.Count; i++)
                {
                    Contacts.Add(new ListViewPlayItem(HyPlayList.List[i].Name, i, HyPlayList.List[i].AudioInfo.Artist));
                }

                ListBoxPlayList.ItemsSource = Contacts;
                ListBoxPlayList.SelectedIndex = HyPlayList.NowPlaying;
            }
            catch
            {
            }
        }

        private void BtnPlayStateChange_OnClick(object sender, RoutedEventArgs e)
        {
            if (HyPlayList.isPlaying)
            {
                HyPlayList.Player.Pause();
            }
            else if (!HyPlayList.isPlaying)
            {
                HyPlayList.Player.Play();
            }

            PlayStateIcon.Glyph = HyPlayList.isPlaying ? "\uEDB5" : "\uEDB4";
        }

        private void SliderAudioRate_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            HyPlayList.Player.Volume = e.NewValue / 100;
            if (Common.PageExpandedPlayer != null)
            {
                Common.PageExpandedPlayer.SliderVolumn.Value = e.NewValue;
            }
        }

        private void BtnMute_OnCllick(object sender, RoutedEventArgs e)
        {
            HyPlayList.Player.IsMuted = !HyPlayList.Player.IsMuted;
            BtnMuteIcon.Glyph = HyPlayList.Player.IsMuted ? "\uE198" : "\uE15D";
            //SliderAudioRate.Visibility = HyPlayList.Player.IsMuted ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnPreviousSong_OnClick(object sender, RoutedEventArgs e)
        {
            if (Common.GLOBAL["PERSONALFM"].ToString() == "true")
            {
                PersonalFM.ExitFm();
            }
            else
            {
                HyPlayList.SongMovePrevious();
            }
        }

        private void BtnNextSong_OnClick(object sender, RoutedEventArgs e)
        {
            HyPlayList.SongMoveNext();
        }

        private void ListBoxPlayList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxPlayList.SelectedIndex != -1 && ListBoxPlayList.SelectedIndex != HyPlayList.NowPlaying)
            {
                HyPlayList.SongMoveTo(ListBoxPlayList.SelectedIndex);
            }
        }

        private void ButtonExpand_OnClick(object sender, RoutedEventArgs e)
        {
            ButtonExpand.Visibility = Visibility.Collapsed;
            ButtonCollapse.Visibility = Visibility.Visible;
            Common.PageMain.GridPlayBar.Background = null;
            //Common.PageMain.MainFrame.Visibility = Visibility.Collapsed;
            Common.PageMain.ExpandedPlayer.Visibility = Visibility.Visible;
            Common.PageMain.ExpandedPlayer.Navigate(typeof(ExpandedPlayer), null,
                new EntranceNavigationTransitionInfo());
            if (Common.Setting.expandAnimation && GridSongInfoContainer.Visibility == Visibility.Visible)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("SongTitle", TbSongName);
                if (GridSongInfoContainer.Visibility == Visibility.Visible)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("SongImg", AlbumImage);
                }

                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("SongArtist", TbSingerName);
                Common.PageExpandedPlayer.StartExpandAnimation();
            }

            GridSongInfo.Visibility = Visibility.Collapsed;
            GridSongAdvancedOperation.Visibility = Visibility.Visible;
        }

        public void ButtonCollapse_OnClick(object sender, RoutedEventArgs e)
        {
            Common.PageExpandedPlayer.StartCollapseAnimation();
            GridSongAdvancedOperation.Visibility = Visibility.Collapsed;
            GridSongInfo.Visibility = Visibility.Visible;
            if (Common.Setting.expandAnimation && GridSongInfoContainer.Visibility == Visibility.Visible)
            {
                ConnectedAnimation anim1 = null;
                ConnectedAnimation anim2 = null;
                ConnectedAnimation anim3 = null;
                anim1 = ConnectedAnimationService.GetForCurrentView().GetAnimation("SongTitle");
                anim2 = ConnectedAnimationService.GetForCurrentView().GetAnimation("SongImg");
                anim3 = ConnectedAnimationService.GetForCurrentView().GetAnimation("SongArtist");
                anim3.Configuration = new DirectConnectedAnimationConfiguration();
                if (anim2 != null)
                {
                    anim2.Configuration = new DirectConnectedAnimationConfiguration();
                }

                anim1.Configuration = new DirectConnectedAnimationConfiguration();
                try
                {
                    anim3?.TryStart(TbSingerName);
                    anim1?.TryStart(TbSongName);
                    anim2?.TryStart(AlbumImage);
                }
                catch
                {
                    //ignore
                }
            }

            ButtonExpand.Visibility = Visibility.Visible;
            ButtonCollapse.Visibility = Visibility.Collapsed;
            Common.PageExpandedPlayer.Dispose();
            Common.PageExpandedPlayer = null;
            Common.PageMain.ExpandedPlayer.Navigate(typeof(BlankPage));
            //Common.PageMain.MainFrame.Visibility = Visibility.Visible;
            Common.PageMain.ExpandedPlayer.Visibility = Visibility.Collapsed;
            Common.PageMain.GridPlayBar.Background =
                Application.Current.Resources["SystemControlAcrylicElementMediumHighBrush"] as Brush;
            Window.Current.SetTitleBar(Common.PageBase.AppTitleBar);
        }

        private void ButtonCleanAll_OnClick(object sender, RoutedEventArgs e)
        {
            HyPlayList.RemoveAllSong();
            ListBoxPlayList.ItemsSource = new ObservableCollection<ListViewPlayItem>();
        }

        private void ButtonAddLocal_OnClick(object sender, RoutedEventArgs e)
        {
            TestFile();
        }

        private void SliderProgress_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (canslide)
            {
                HyPlayList.Player.PlaybackSession.Position = TimeSpan.FromMilliseconds(SliderProgress.Value);
            }
        }

        private void PlayListRemove_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn)
                {
                    HyPlayList.RemoveSong(int.Parse(btn.Tag.ToString()));
                    RefreshSongList();
                }
            }
            catch
            {
            }
        }

        private void BtnPlayRollType_OnClick(object sender, RoutedEventArgs e)
        {
            if (Common.GLOBAL["PERSONALFM"].ToString() != "true")
            {
                switch (NowPlayType)
                {
                    case PlayMode.DefaultRoll:
                        //变成随机
                        HyPlayList.NowPlayType = PlayMode.Shuffled;
                        NowPlayType = PlayMode.Shuffled;
                        IconPlayType.Glyph = "\uE14B";
                        break;
                    case PlayMode.Shuffled:
                        //变成单曲
                        IconPlayType.Glyph = "\uE1CC";
                        HyPlayList.NowPlayType = PlayMode.SinglePlay;
                        NowPlayType = PlayMode.SinglePlay;
                        break;
                    case PlayMode.SinglePlay:
                        //变成顺序
                        HyPlayList.NowPlayType = PlayMode.DefaultRoll;
                        NowPlayType = PlayMode.DefaultRoll;
                        IconPlayType.Glyph = "\uE169";
                        break;
                }
            }
            else
            {
                Common.ncapi.RequestAsync(CloudMusicApiProviders.FmTrash,
                    new Dictionary<string, object>() { { "id", HyPlayList.NowPlayingItem.NcPlayItem.id } });
                PersonalFM.LoadNextFM();
            }
        }

        public PlayMode NowPlayType = PlayMode.DefaultRoll;

        private void BtnLike_OnClick(object sender, RoutedEventArgs e)
        {
            if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.Netease)
            {
                Api.LikeSong(HyPlayList.NowPlayingItem.NcPlayItem.id,
                    !Common.LikedSongs.Contains(HyPlayList.NowPlayingItem.NcPlayItem.id));
                if (Common.LikedSongs.Contains(HyPlayList.NowPlayingItem.NcPlayItem.id))
                {
                    Common.LikedSongs.Remove(HyPlayList.NowPlayingItem.NcPlayItem.id);
                }
                else
                {
                    Common.LikedSongs.Add(HyPlayList.NowPlayingItem.NcPlayItem.id);
                }

                BtnLike.IsChecked = Common.LikedSongs.Contains(HyPlayList.NowPlayingItem.NcPlayItem.id);
            }
            else if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.FM)
            {
                Common.ncapi.RequestAsync(CloudMusicApiProviders.ResourceLike,
                    new Dictionary<string, object>()
                        { { "type", "4" }, { "t", "1" }, { "id", HyPlayList.NowPlayingItem.NcPlayItem.id } });
            }
            else
            {
                BtnLike.IsChecked = false;
            }
        }

        private void ImageContainer_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ImageContainer.BorderBrush =
                Application.Current.Resources["SystemControlBackgroundListMediumRevealBorderBrush"] as Brush;
        }

        private void ImageContainer_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            ImageContainer.BorderBrush = null;
        }

        private void ImageContainer_OnTapped(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            ButtonExpand_OnClick(sender, null);
        }

        private async void TbSingerName_OnTapped(object sender, TappedRoutedEventArgs tappedRoutedEventArgs)
        {
            try
            {
                if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.Netease)
                {
                    if (HyPlayList.NowPlayingItem.NcPlayItem.Artist.Count > 1)
                    {
                        await new ArtistSelectDialog(HyPlayList.NowPlayingItem.NcPlayItem.Artist).ShowAsync();
                    }
                    else
                    {
                        Common.BaseFrame.Navigate(typeof(ArtistPage),
                            HyPlayList.NowPlayingItem.NcPlayItem.Artist[0].id);
                    }

                    ButtonCollapse_OnClick(this, null);
                }
                else
                {
                    //TODO: 增加跳转到电台
                }
            }
            catch
            {
            }
        }

        private async void Btn_Sub_OnClick(object sender, RoutedEventArgs e)
        {
            if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.Netease)
            {
                await new SongListSelect(HyPlayList.NowPlayingItem.NcPlayItem.id).ShowAsync();
            }
        }

        private void Btn_Down_OnClick(object sender, RoutedEventArgs e)
        {
            if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.Netease)
            {
                DownloadManager.AddDownload(HyPlayList.NowPlayingItem.ToNCSong());
            }
            else if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.FM)
            {
                //TODO: 电台的下载操作
            }
        }

        private void Btn_Comment_OnClick(object sender, RoutedEventArgs e)
        {
            if (HyPlayList.NowPlayingItem.ItemType == HyPlayItemType.Netease)
            {
                Common.BaseFrame.Navigate(typeof(Comments), (object)HyPlayList.NowPlayingItem.ToNCSong());
            }
            else
            {
                Common.BaseFrame.Navigate(typeof(Comments), "fm" + HyPlayList.NowPlayingItem.NcPlayItem.id);
            }

            ButtonCollapse_OnClick(this, e);
        }

        private void Btn_Share_OnClick(object sender, RoutedEventArgs e)
        {
            //TODO: 分享电台节目
            if (HyPlayList.NowPlayingItem.ItemType != HyPlayItemType.Netease) return;
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();

            dataTransferManager.DataRequested += ((manager, args) =>
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetWebLink(new Uri("https://music.163.com/#/song?id=" +
                                               HyPlayList.NowPlayingItem.NcPlayItem.id));
                dataPackage.Properties.Title = HyPlayList.NowPlayingItem.Name;
                dataPackage.Properties.Description =
                    "歌手: " + string.Join(';',
                        HyPlayList.NowPlayingItem.NcPlayItem.Artist
                            .Select(t => t.name));
                DataRequest request = args.Request;
                request.Data = dataPackage;
            });

            //展示系统的共享ui
            DataTransferManager.ShowShareUI();
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeDesktopLyric();
        }

        private void BtnPlayStateChange_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            BtnPlayStateChange_OnClick(sender, e);
        }
    }


    public class ListViewPlayItem
    {
        public string Name { get; private set; }
        public string Artist { get; private set; }
        public string DisplayName => Artist + " - " + Name;

        public int index { get; private set; }

        public ListViewPlayItem(string name, int index, string artist)
        {
            Name = name;
            Artist = artist;
            this.index = index;
        }

        public override string ToString()
        {
            return Artist + " - " + Name;
        }
    }

    public class ThumbConverter : DependencyObject, IValueConverter
    {
        public double SecondValue
        {
            get => (double)GetValue(SecondValueProperty);
            set => SetValue(SecondValueProperty, value);
        }

        // Using a DependencyProperty as the backing store for SecondValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SecondValueProperty =
            DependencyProperty.Register("SecondValue", typeof(double), typeof(ThumbConverter),
                new PropertyMetadata(0d));


        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // assuming you want to display precentages

            return TimeSpan.FromMilliseconds(double.Parse(value.ToString())).ToString(@"hh\:mm\:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}