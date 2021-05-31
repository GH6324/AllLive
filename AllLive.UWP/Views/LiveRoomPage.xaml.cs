﻿using AllLive.UWP.Models;
using AllLive.UWP.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AllLive.Core.Models;
using AllLive.UWP.Helper;
using Microsoft.UI.Xaml.Controls;
using NSDanmaku.Model;
using Windows.Media.Playback;
using Windows.UI.ViewManagement;
using Windows.UI.Popups;
using FFmpegInterop;
using Windows.System.Display;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Display;
using Windows.UI;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace AllLive.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LiveRoomPage : Page
    {
        readonly LiveRoomVM liveRoomVM;
        readonly SettingVM settingVM;
        readonly MediaPlayer mediaPlayer;
        readonly FFmpegInteropConfig _config;
        FFmpegInterop.FFmpegInteropMSS interopMSS;
        DisplayRequest dispRequest;
        PageArgs pageArgs;
        //当前处于小窗
        private bool isMini = false;
        DispatcherTimer timer_focus;
        DispatcherTimer controlTimer;
       
        public LiveRoomPage()
        {
            this.InitializeComponent();
            settingVM = new SettingVM();
            liveRoomVM = new LiveRoomVM(settingVM);
           
            dispRequest = new DisplayRequest();
            _config = new FFmpegInteropConfig();
            _config.FFmpegOptions.Add("rtsp_transport", "tcp");
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            liveRoomVM.ChangedPlayUrl += LiveRoomVM_ChangedPlayUrl;
            liveRoomVM.AddDanmaku += LiveRoomVM_AddDanmaku;
            //每过2秒就设置焦点
            timer_focus = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(2) };
            timer_focus.Tick += Timer_focus_Tick;
            controlTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
            controlTimer.Tick += ControlTimer_Tick;
            mediaPlayer = new MediaPlayer();
            mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            mediaPlayer.PlaybackSession.BufferingStarted += PlaybackSession_BufferingStarted;
            mediaPlayer.PlaybackSession.BufferingProgressChanged += PlaybackSession_BufferingProgressChanged;
            mediaPlayer.PlaybackSession.BufferingEnded += PlaybackSession_BufferingEnded;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded; ;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

            timer_focus.Start();
            controlTimer.Start();

            LoadSetting();
        }

        private void LiveRoomVM_AddDanmaku(object sender, string e)
        {
            if (DanmuControl.Visibility == Visibility.Visible)
            {
               
                DanmuControl.AddLiveDanmu(e, false, Colors.White);
            }
        }

        private async void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            var elent = FocusManager.GetFocusedElement();
            if (elent is TextBox || elent is AutoSuggestBox)
            {
                args.Handled = false;
                return;
            }
            args.Handled = true;
            switch (args.VirtualKey)
            {
                //case Windows.System.VirtualKey.Space:
                //    if (mediaPlayer.PlaybackSession.CanPause)
                //    {
                //        mediaPlayer.Pause();
                //    }
                //    else
                //    {
                //        mediaPlayer.Play();
                //    }
                //    break;

                case Windows.System.VirtualKey.Up:
                    mediaPlayer.Volume += 0.1;
                    TxtToolTip.Text = "音量:" + mediaPlayer.Volume.ToString("P");
                    ToolTip.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;

                case Windows.System.VirtualKey.Down:
                    mediaPlayer.Volume -= 0.1;
                    if (mediaPlayer.Volume == 0)
                    {
                        TxtToolTip.Text = "静音";
                    }
                    else
                    {
                        TxtToolTip.Text = "音量:" + mediaPlayer.Volume.ToString("P");
                    }
                    ToolTip.Visibility = Visibility.Visible;
                    await Task.Delay(2000);
                    ToolTip.Visibility = Visibility.Collapsed;
                    break;
                case Windows.System.VirtualKey.Escape:
                    SetFullScreen(false);

                    break;
                case Windows.System.VirtualKey.F8:
                case Windows.System.VirtualKey.T:
                    //小窗播放
                    MiniWidnows(BottomBtnExitMiniWindows.Visibility == Visibility.Visible);

                    break;
                case Windows.System.VirtualKey.F12:
                case Windows.System.VirtualKey.W:
                    SetFullWindow(PlayBtnFullWindow.Visibility == Visibility.Visible);
                    break;
                case Windows.System.VirtualKey.F11:
                case Windows.System.VirtualKey.F:
                case Windows.System.VirtualKey.Enter:
                    SetFullScreen(PlayBtnFullScreen.Visibility == Visibility.Visible);
                    break;
                case Windows.System.VirtualKey.F10:
                    await CaptureVideo();
                    break;
                case Windows.System.VirtualKey.F9:
                case Windows.System.VirtualKey.D:
                    //if (DanmuControl.Visibility == Visibility.Visible)
                    //{
                    //    DanmuControl.Visibility = Visibility.Collapsed;

                    //}
                    //else
                    //{
                    //    DanmuControl.Visibility = Visibility.Visible;
                    //}
                    PlaySWDanmu.IsOn = DanmuControl.Visibility != Visibility.Visible;
                    break;

                default:
                    break;
            }
        }


        private async void LiveRoomVM_ChangedPlayUrl(object sender, string e)
        {
            await SetPlayer(e);
        }
        private async Task SetPlayer(string url)
        {
            try
            {
                PlayerLoading.Visibility = Visibility.Visible;
                PlayerLoadText.Text = "加载中";
                if (mediaPlayer != null)
                {
                    mediaPlayer.Pause();
                    mediaPlayer.Source = null;
                }
                if (interopMSS != null)
                {
                    interopMSS.Dispose();
                    interopMSS = null;
                }
                interopMSS = await FFmpegInteropMSS.CreateFromUriAsync(url, _config);
                mediaPlayer.AutoPlay = true;
                mediaPlayer.Source = interopMSS.CreateMediaPlaybackItem();
                player.SetMediaPlayer(mediaPlayer);
            }
            catch (Exception ex)
            {
                Utils.ShowMessageToast("播放失败" + ex.Message);
            }

        }
        private void StopPlay()
        {
           
            timer_focus.Stop();
            controlTimer.Stop();
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            if (mediaPlayer != null)
            {
                mediaPlayer.Pause();
                mediaPlayer.Source = null;
            }
            if (interopMSS != null)
            {
                interopMSS.Dispose();
                interopMSS = null;
            }
            liveRoomVM?.Stop();
            //取消屏幕常亮
            if (dispRequest != null)
            {
                dispRequest = null;
            }
         
            SetFullScreen(false);
            MiniWidnows(false);
        }
        private void ControlTimer_Tick(object sender, object e)
        {
            if (showControlsFlag != -1)
            {
                if (showControlsFlag >= 5)
                {
                    var elent = FocusManager.GetFocusedElement();
                    if (!(elent is TextBox) && !(elent is AutoSuggestBox))
                    {
                        ShowControl(false);
                        showControlsFlag = -1;
                    }
                }
                else
                {
                    showControlsFlag++;
                }
            }
        }

        private void Timer_focus_Tick(object sender, object e)
        {
            var elent = FocusManager.GetFocusedElement();
            if (elent is Button || elent is AppBarButton || elent is HyperlinkButton || elent is MenuFlyoutItem)
            {
                BtnFoucs.Focus(FocusState.Programmatic);
            }

        }
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                StopPlay();
                Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;

            }
               
            base.OnNavigatingFrom(e);
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if(e.NavigationMode== NavigationMode.New)
            {
                pageArgs = e.Parameter as PageArgs;
                var siteInfo = MainVM.Sites.FirstOrDefault(x => x.LiveSite.Equals(pageArgs.Site));
                if (siteInfo.Name == "哔哩哔哩直播")
                {
                    _config.FFmpegOptions.Add("user_agent", "Mozilla/5.0 BiliDroid/1.12.0 (bbcallen@gmail.com)");
                    _config.FFmpegOptions.Add("referer", "https://live.bilibili.com/");
                }
                liveRoomVM.SiteLogo = siteInfo.Logo;
                liveRoomVM.SiteName = siteInfo.Name;
                var data = pageArgs.Data as LiveRoomItem;
                liveRoomVM.LoadData(pageArgs.Site,data.RoomID);
               
            }
        }

        private async Task CaptureVideo()
        {
            try
            {
                string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg";
                StorageFolder applicationFolder = KnownFolders.PicturesLibrary;
                StorageFolder folder = await applicationFolder.CreateFolderAsync("直播截图", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                RenderTargetBitmap bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(player);
                var pixelBuffer = await bitmap.GetPixelsAsync();
                using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Ignore,
                         (uint)bitmap.PixelWidth,
                         (uint)bitmap.PixelHeight,
                         DisplayInformation.GetForCurrentView().LogicalDpi,
                         DisplayInformation.GetForCurrentView().LogicalDpi,
                         pixelBuffer.ToArray());
                    await encoder.FlushAsync();
                }
                Utils.ShowMessageToast("截图已经保存至图片库");
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("截图失败");
            }
        }



        private void LoadSetting()
        {
            //右侧宽度
            var width = SettingHelper.GetValue<double>(SettingHelper.RIGHT_DETAIL_WIDTH, 280);
            ColumnRight.Width = new GridLength(width, GridUnitType.Pixel);
            GridRight.SizeChanged += new SizeChangedEventHandler((sender, args) => {
                if (args.NewSize.Width<=0)
                {
                    return;
                }
                SettingHelper.SetValue<double>(SettingHelper.RIGHT_DETAIL_WIDTH, args.NewSize.Width);
            });
            //软解视频
            swSoftwareDecode.IsOn = SettingHelper.GetValue<bool>(SettingHelper.SORTWARE_DECODING, false);
            if (swSoftwareDecode.IsOn)
            {
                _config.VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder;
            }
            else
            {
                _config.VideoDecoderMode = VideoDecoderMode.Automatic;
               
            }
            swSoftwareDecode.Loaded += new RoutedEventHandler((sender, e) =>
            {
                swSoftwareDecode.Toggled += new RoutedEventHandler((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.SORTWARE_DECODING, swSoftwareDecode.IsOn);
                    if (swSoftwareDecode.IsOn)
                    {
                        _config.VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder;
                    }
                    else
                    {
                        _config.VideoDecoderMode = VideoDecoderMode.Automatic;
                    }
                    Utils.ShowMessageToast("更改清晰度或刷新后生效");
                });
            });
            //弹幕开关
            var state = SettingHelper.GetValue<Visibility>(SettingHelper.LiveDanmaku.SHOW, Visibility.Visible) ;
            DanmuControl.Visibility = state;
            PlaySWDanmu.IsOn = state == Visibility.Visible;
            PlaySWDanmu.Toggled += new RoutedEventHandler((e, args) =>
            {
                var visibility = PlaySWDanmu.IsOn ? Visibility.Visible : Visibility.Collapsed;
                DanmuControl.Visibility = visibility;
                SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHOW, visibility);
            });
           
            //音量
            mediaPlayer.Volume = SettingHelper.GetValue<double>(SettingHelper.PLAYER_VOLUME, 1.0);
            SliderVolume.Value = mediaPlayer.Volume;
            SliderVolume.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                mediaPlayer.Volume = SliderVolume.Value;
                SettingHelper.SetValue<double>(SettingHelper.PLAYER_VOLUME, SliderVolume.Value);
            });
            //亮度
            _brightness = SettingHelper.GetValue<double>(SettingHelper.PLAYER_BRIGHTNESS, 0);
            BrightnessShield.Opacity = _brightness;

            //弹幕清理
            numCleanCount.Value = SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.DANMU_CLEAN_COUNT, 200);
            numCleanCount.Loaded += new RoutedEventHandler((sender, e) =>
            {
                numCleanCount.ValueChanged += new TypedEventHandler<NumberBox, NumberBoxValueChangedEventArgs>((obj, args) =>
                {
                    liveRoomVM.MessageCleanCount = SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.DANMU_CLEAN_COUNT, Convert.ToInt32(args.NewValue));
                    SettingHelper.SetValue(SettingHelper.LiveDanmaku.DANMU_CLEAN_COUNT, Convert.ToInt32(args.NewValue));
                });
            });

            //互动文字大小
            numFontsize.Value = SettingHelper.GetValue<double>(SettingHelper.MESSAGE_FONTSIZE, 14.0);
            numFontsize.Loaded += new RoutedEventHandler((sender, e) =>
            {
                numFontsize.ValueChanged += new TypedEventHandler<NumberBox, NumberBoxValueChangedEventArgs>((obj, args) =>
                {
                    SettingHelper.SetValue(SettingHelper.MESSAGE_FONTSIZE, args.NewValue);
                });
            });


            //弹幕关键词
            LiveDanmuSettingListWords.ItemsSource = settingVM.ShieldWords;

            //弹幕顶部距离
            DanmuControl.Margin = new Thickness(0, SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.TOP_MARGIN, 0), 0, 0);
            DanmuTopMargin.Value = DanmuControl.Margin.Top;
            DanmuTopMargin.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.LiveDanmaku.TOP_MARGIN, DanmuTopMargin.Value);
                DanmuControl.Margin = new Thickness(0, DanmuTopMargin.Value, 0, 0);
            });
            //弹幕大小
            DanmuControl.DanmakuSizeZoom = SettingHelper.GetValue<double>(SettingHelper.LiveDanmaku.FONT_ZOOM, 1);
            DanmuSettingFontZoom.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                if (isMini) return;
                SettingHelper.SetValue<double>(SettingHelper.LiveDanmaku.FONT_ZOOM, DanmuSettingFontZoom.Value);
            });
            //弹幕速度
            DanmuControl.DanmakuDuration = SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.SPEED, 10);
            DanmuSettingSpeed.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                if (isMini) return;
                SettingHelper.SetValue<double>(SettingHelper.LiveDanmaku.SPEED, DanmuSettingSpeed.Value);
            });
            //弹幕透明度
            DanmuControl.Opacity = SettingHelper.GetValue<double>(SettingHelper.LiveDanmaku.OPACITY, 1.0);
            DanmuSettingOpacity.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.LiveDanmaku.OPACITY, DanmuSettingOpacity.Value);
            });
            //弹幕加粗
            DanmuControl.DanmakuBold = SettingHelper.GetValue<bool>(SettingHelper.LiveDanmaku.BOLD, false);
            DanmuSettingBold.Toggled += new RoutedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<bool>(SettingHelper.LiveDanmaku.BOLD, DanmuSettingBold.IsOn);
            });
            //弹幕样式
            DanmuControl.DanmakuStyle = (DanmakuBorderStyle)SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.BORDER_STYLE, 2);
            DanmuSettingStyle.SelectionChanged += new SelectionChangedEventHandler((e, args) =>
            {
                if (DanmuSettingStyle.SelectedIndex != -1)
                {
                    SettingHelper.SetValue<int>(SettingHelper.LiveDanmaku.BORDER_STYLE, DanmuSettingStyle.SelectedIndex);
                }
            });


            //弹幕显示区域
            DanmuControl.DanmakuArea = SettingHelper.GetValue<double>(SettingHelper.LiveDanmaku.AREA, 1);
            DanmuSettingArea.ValueChanged += new RangeBaseValueChangedEventHandler((e, args) =>
            {
                SettingHelper.SetValue<double>(SettingHelper.LiveDanmaku.AREA, DanmuSettingArea.Value);
            });
        }

        private void RemoveLiveDanmuWord_Click(object sender, RoutedEventArgs e)
        {
            var word = (sender as AppBarButton).DataContext as string;
            settingVM.ShieldWords.Remove(word);
            SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHIELD_WORD, settingVM.ShieldWords);
        }

        private void LiveDanmuSettingTxtWord_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrEmpty(LiveDanmuSettingTxtWord.Text))
            {
                Utils.ShowMessageToast("关键字不能为空");
                return;
            }
            if (!settingVM.ShieldWords.Contains(LiveDanmuSettingTxtWord.Text))
            {
                settingVM.ShieldWords.Add(LiveDanmuSettingTxtWord.Text);
                SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHIELD_WORD, settingVM.ShieldWords);
            }

            LiveDanmuSettingTxtWord.Text = "";
            SettingHelper.SetValue(SettingHelper.LiveDanmaku.SHIELD_WORD, settingVM.ShieldWords);
        }

        #region 播放器事件
        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                liveRoomVM.Living = false;
                player.SetMediaPlayer(null);
            });
        }

        private async void PlaybackSession_BufferingEnded(MediaPlaybackSession sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayerLoading.Visibility = Visibility.Collapsed;
            });

        }

        private async void PlaybackSession_BufferingProgressChanged(MediaPlaybackSession sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayerLoadText.Text = sender.BufferingProgress.ToString("p");
            });
        }

        private async void PlaybackSession_BufferingStarted(MediaPlaybackSession sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayerLoading.Visibility = Visibility.Visible;
                PlayerLoadText.Text = "缓冲中";
            });
        }

        private async void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                LogHelper.Log("直播加载失败", LogType.ERROR, new Exception(args.ErrorMessage));
                await new MessageDialog($"啊，直播加载失败了\r\n错误信息:{args.ErrorMessage}\r\n请尝试在直播设置中打开/关闭硬解试试", "播放失败").ShowAsync();
            });

        }

        private async void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //保持屏幕常亮
                dispRequest.RequestActive();
                PlayerLoading.Visibility = Visibility.Collapsed;
                SetMediaInfo();
            });
        }
       
        private async void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.None:
                        break;
                    case MediaPlaybackState.Opening:
                        PlayerLoading.Visibility = Visibility.Visible;
                        PlayerLoadText.Text = "加载中";
                        break;
                    case MediaPlaybackState.Buffering:
                        PlayerLoading.Visibility = Visibility.Visible;
                        break;
                    case MediaPlaybackState.Playing:
                        PlayBtnPlay.Visibility = Visibility.Collapsed;
                        PlayBtnPause.Visibility = Visibility.Visible;
                        break;
                    case MediaPlaybackState.Paused:
                        PlayBtnPlay.Visibility = Visibility.Visible;
                        PlayBtnPause.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        break;
                }
            });
        }


        private void SetMediaInfo()
        {
            try
            {
                var str = $"Url: {liveRoomVM.CurrentLine?.Url??""}\r\n";
                str += $"Quality: {liveRoomVM.CurrentQuality?.Quality??""}\r\n";
                str += $"Video Codec: {interopMSS.CurrentVideoStream.CodecName}\r\nAudio Codec:{interopMSS.CurrentAudioStream.CodecName}\r\n";
                str += $"Resolution: {interopMSS.CurrentVideoStream.PixelWidth} x {interopMSS.CurrentVideoStream.PixelHeight}\r\n";
                str += $"FPS: {interopMSS.CurrentVideoStream.FramesPerSecond}\r\n";
                str += $"Video Bitrate: {interopMSS.CurrentVideoStream.Bitrate / 1024} Kbps\r\n";
                str += $"Audio Bitrate: {interopMSS.AudioStreams[0].Bitrate / 1024} Kbps\r\n";
                str += $"Decoder Engine: {interopMSS.CurrentVideoStream.DecoderEngine.ToString()}";
                txtInfo.Text = str;
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"读取信息失败\r\n{ex.Message}";
            }



        }

        #endregion


        #region 手势
        int showControlsFlag = 0;
        bool pointer_in_player = false;

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ShowControl(control.Visibility == Visibility.Collapsed);

        }
        bool runing = false;
        private async void ShowControl(bool show)
        {
            if (runing) return;
            runing = true;
            if (show)
            {
                showControlsFlag = 0;
                control.Visibility = Visibility.Visible;

                await control.FadeInAsync(280);

            }
            else
            {
                if (pointer_in_player)
                {
                    Window.Current.CoreWindow.PointerCursor = null;
                }
                await control.FadeOutAsync(280);
                control.Visibility = Visibility.Collapsed;
            }
            runing = false;
        }
        private void Grid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (PlayBtnFullScreen.Visibility == Visibility.Visible)
            {
                PlayBtnFullScreen_Click(sender, null);
            }
            else
            {

                PlayBtnExitFullScreen_Click(sender, null);
            }
        }
        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            pointer_in_player = true;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            pointer_in_player = false;
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
        }

        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (Window.Current.CoreWindow.PointerCursor == null)
            {
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            }

        }

        bool ManipulatingBrightness = false;
        private void Grid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;
            //progress.Visibility = Visibility.Visible;
            if (ManipulatingBrightness)
                HandleSlideBrightnessDelta(e.Delta.Translation.Y);
            else
                HandleSlideVolumeDelta(e.Delta.Translation.Y);
        }


        private void HandleSlideVolumeDelta(double delta)
        {
            if (delta > 0)
            {
                double dd = delta / (this.ActualHeight * 0.8);

                //slider_V.Value -= d;
                var volume = mediaPlayer.Volume - dd;
                if (volume < 0) volume = 0;
                SliderVolume.Value = volume;

            }
            else
            {
                double dd = Math.Abs(delta) / (this.ActualHeight * 0.8);
                var volume = mediaPlayer.Volume + dd;
                if (volume > 1) volume = 1;
                SliderVolume.Value = volume;
                //slider_V.Value += d;
            }
            TxtToolTip.Text = "音量:" + mediaPlayer.Volume.ToString("P");

            //Utils.ShowMessageToast("音量:" +  mediaElement.MediaPlayer.Volume.ToString("P"), 3000);
        }
        private void HandleSlideBrightnessDelta(double delta)
        {
            double dd = Math.Abs(delta) / (this.ActualHeight * 0.8);
            if (delta > 0)
            {
                Brightness = Math.Min(Brightness + dd, 1);
            }
            else
            {
                Brightness = Math.Max(Brightness - dd, 0);
            }
            TxtToolTip.Text = "亮度:" + Math.Abs(Brightness - 1).ToString("P");
        }
        private void Grid_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = true;
            TxtToolTip.Text = "";
            ToolTip.Visibility = Visibility.Visible;

            if (e.Position.X < this.ActualWidth / 2)
                ManipulatingBrightness = true;
            else
                ManipulatingBrightness = false;

        }

        double _brightness;
        double Brightness
        {
            get => _brightness;
            set
            {
                _brightness = value;
                BrightnessShield.Opacity = value;
                SettingHelper.SetValue<double>(SettingHelper.PLAYER_BRIGHTNESS, _brightness);
            }
        }

        private void Grid_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;
            ToolTip.Visibility = Visibility.Collapsed;
        }
        #endregion
        #region 窗口操作
        private void PlayBtnFullScreen_Click(object sender, RoutedEventArgs e)
        {
            SetFullScreen(true);
        }

        private void PlayBtnExitFullScreen_Click(object sender, RoutedEventArgs e)
        {
            SetFullScreen(false);
        }

        private void PlayBtnExitFullWindow_Click(object sender, RoutedEventArgs e)
        {
            SetFullWindow(false);
        }

        private void PlayBtnFullWindow_Click(object sender, RoutedEventArgs e)
        {
            SetFullWindow(true);
        }

        private void PlayBtnMinWindow_Click(object sender, RoutedEventArgs e)
        {
            MiniWidnows(true);
        }
        private void SetFullWindow(bool e)
        {

            if (e)
            {
                PlayBtnFullWindow.Visibility = Visibility.Collapsed;
                PlayBtnExitFullWindow.Visibility = Visibility.Visible;
                ColumnRight.Width = new GridLength(0, GridUnitType.Pixel);
                ColumnRight.MinWidth = 0;
                BottomInfo.Height = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                PlayBtnFullWindow.Visibility = Visibility.Visible;
                PlayBtnExitFullWindow.Visibility = Visibility.Collapsed;
                ColumnRight.Width = new GridLength(SettingHelper.GetValue<double>(SettingHelper.RIGHT_DETAIL_WIDTH,280), GridUnitType.Pixel);
                ColumnRight.MinWidth = 100;
                BottomInfo.Height = GridLength.Auto;
            }
        }
        private void SetFullScreen(bool e)
        {
            ApplicationView view = ApplicationView.GetForCurrentView();
            if (e)
            {
                PlayBtnFullScreen.Visibility = Visibility.Collapsed;
                PlayBtnExitFullScreen.Visibility = Visibility.Visible;
                
                ColumnRight.Width = new GridLength(0, GridUnitType.Pixel);
                ColumnRight.MinWidth = 0;
                BottomInfo.Height = new GridLength(0, GridUnitType.Pixel);
                //全屏
                if (!view.IsFullScreenMode)
                {
                    view.TryEnterFullScreenMode();
                }
            }
            else
            {
                PlayBtnFullScreen.Visibility = Visibility.Visible;
                PlayBtnExitFullScreen.Visibility = Visibility.Collapsed;

                ColumnRight.Width = new GridLength(280, GridUnitType.Pixel);
                ColumnRight.MinWidth = 100;
                BottomInfo.Height = GridLength.Auto;
                //退出全屏
                if (view.IsFullScreenMode)
                {
                    view.ExitFullScreenMode();
                }
            }
        }
        private async void MiniWidnows(bool mini)
        {
            isMini = mini;
            ApplicationView view = ApplicationView.GetForCurrentView();
            if (mini)
            {
                SetFullWindow(true);
                StandardControl.Visibility = Visibility.Collapsed;
                MiniControl.Visibility = Visibility.Visible;

                if (ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
                {
                    await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
                    DanmuControl.DanmakuSizeZoom = 0.5;
                    DanmuControl.DanmakuDuration = 6;
                    DanmuControl.ClearAll();
                }
            }
            else
            {
                SetFullWindow(false);
                StandardControl.Visibility = Visibility.Visible;
                MiniControl.Visibility = Visibility.Collapsed;
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
                DanmuControl.DanmakuSizeZoom = SettingHelper.GetValue<double>(SettingHelper.LiveDanmaku.FONT_ZOOM, 1);
                DanmuControl.DanmakuDuration = SettingHelper.GetValue<int>(SettingHelper.LiveDanmaku.SPEED, 10);
                DanmuControl.ClearAll();
                DanmuControl.Visibility = SettingHelper.GetValue<Visibility>(SettingHelper.LiveDanmaku.SHOW, Visibility.Visible);
            }
           
        }
        private void BottomBtnExitMiniWindows_Click(object sender, RoutedEventArgs e)
        {
            MiniWidnows(false);
        }

        private async void PlayTopBtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            await CaptureVideo();
        }

        private void PlayBtnPlay_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Play();
        }

        private void PlayBtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
        }


        #endregion

        private void BottomBtnShare_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.detail==null)
            {
                return;
            }
            Utils.SetClipboard(liveRoomVM.detail.Url);
            Utils.ShowMessageToast("已复制链接到剪切板");
        }

        private async void BottomBtnOpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.detail == null)
            {
                return;
            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri(liveRoomVM.detail.Url));
        }

        private void BottomBtnPlayUrl_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.CurrentLine == null)
            {
                return;
            }
            Utils.SetClipboard(liveRoomVM.CurrentLine.Url);
            Utils.ShowMessageToast("已复制链接到剪切板");
        }

        private void BottomBtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (liveRoomVM.Loading) return;
            if (mediaPlayer != null)
            {
                mediaPlayer.Pause();
                mediaPlayer.Source = null;
            }
            if (interopMSS != null)
            {
                interopMSS.Dispose();
                interopMSS = null;
            }
            liveRoomVM?.Stop();
            liveRoomVM.LoadData(pageArgs.Site, liveRoomVM.RoomID);
        }
    }
}