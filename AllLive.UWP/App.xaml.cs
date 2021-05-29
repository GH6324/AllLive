﻿using AllLive.UWP.Helper;
using FFmpegInterop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AllLive.UWP
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application, ILogProvider
    {
        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {

            this.InitializeComponent();

            App.Current.UnhandledException += App_UnhandledException;
            FFmpegInteropLogging.SetLogLevel(LogLevel.Info);
            FFmpegInteropLogging.SetLogProvider(this);
            this.Suspending += OnSuspending;
        }
        private void RegisterExceptionHandlingSynchronizationContext()
        {
            ExceptionHandlingSynchronizationContext
                .Register()
                .UnhandledException += SynchronizationContext_UnhandledException;
        }
        private void SynchronizationContext_UnhandledException(object sender, AysncUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                LogHelper.Log("程序运行出现错误", LogType.ERROR, e.Exception);
                Utils.ShowMessageToast("程序出现一个错误，已记录");
            }
            catch (Exception)
            {
            }
        }
        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                LogHelper.Log("程序运行出现错误", LogType.ERROR, e.Exception);
                Utils.ShowMessageToast("程序出现一个错误，已记录");
            }
            catch (Exception)
            {
            }

        }

        public void Log(LogLevel level, string message)
        {
            System.Diagnostics.Debug.WriteLine("FFmpeg ({0}): {1}", level, message);
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
            Frame rootFrame = Window.Current.Content as Frame;

            // 不要在窗口已包含内容时重复应用程序初始化，
            // 只需确保窗口处于活动状态
            if (rootFrame == null)
            {
                // 创建要充当导航上下文的框架，并导航到第一页
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;
                rootFrame.Navigated += RootFrame_Navigated;
                rootFrame.PointerPressed += RootFrame_PointerPressed;
                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: 从之前挂起的应用程序加载状态
                }
                rootFrame.RequestedTheme = (ElementTheme)SettingHelper.GetValue<int>(SettingHelper.THEME, 0);
                // 将框架放在当前窗口中
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // 当导航堆栈尚未还原时，导航到第一页，
                    // 并通过将所需信息作为导航参数传入来配置
                    // 参数
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // 确保当前窗口处于活动状态
                Window.Current.Activate();
            }

            SetTitleBar();
        }

        private void RootFrame_PointerPressed(object sender, PointerRoutedEventArgs e)
        {

            var par = e.GetCurrentPoint(sender as Frame).Properties.PointerUpdateKind;
            if (SettingHelper.GetValue<bool>(SettingHelper.MOUSE_BACK, true) && par == Windows.UI.Input.PointerUpdateKind.XButton1Pressed || par == Windows.UI.Input.PointerUpdateKind.MiddleButtonPressed)
            {
                if ((sender as Frame).CanGoBack)
                {
                    (sender as Frame).GoBack();
                    e.Handled = true;
                }

            }

        }

        public static void SetTitleBar()
        {
            UISettings uISettings = new UISettings();
            var colors = TitltBarButtonColor(uISettings);
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = colors.Item1;
            titleBar.ButtonBackgroundColor = colors.Item2;
            titleBar.BackgroundColor = colors.Item2;
            uISettings.ColorValuesChanged += new TypedEventHandler<UISettings, object>((setting, args) =>
            {
                var color = TitltBarButtonColor(uISettings);
                titleBar.ButtonForegroundColor = color.Item1;
                titleBar.ButtonBackgroundColor = color.Item2;
                titleBar.BackgroundColor = color.Item2;
            });
        }

        private static (Color, Color) TitltBarButtonColor(UISettings uISettings)
        {
            var settingTheme = SettingHelper.GetValue<int>(SettingHelper.THEME, 0);
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var color = uiSettings.GetColorValue(UIColorType.Foreground);
            var bgColor = (color == Colors.White) ? Color.FromArgb(255, 23, 23, 23) : Color.FromArgb(255, 246, 246, 246);
            if (settingTheme != 0)
            {
                color = settingTheme == 1 ? Colors.Black : Colors.White;
                bgColor = settingTheme == 2 ? Color.FromArgb(255, 23, 23, 23) : Color.FromArgb(255, 246, 246, 246);
            }
            return (color, bgColor);
        }
        private void App_BackRequested(object sender, BackRequestedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame.CanGoBack)
            {
                rootFrame.GoBack();
            }
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = (sender as Frame).CanGoBack ?
                AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">有关导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: 保存应用程序状态并停止任何后台活动
            deferral.Complete();
        }
    }
}
