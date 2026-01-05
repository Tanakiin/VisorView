using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.AxHost;
using WinForms = System.Windows.Forms;
using Wpf = System.Windows.Controls;



namespace VisorView
{
        public partial class MainWindow : Window
    {
        const int HOTKEY_ID_TOGGLE_VIS = 0x1001;
        const int HOTKEY_ID_TOGGLE_MODE = 0x1002;

        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const int WM_HOTKEY = 0x0312;

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_NOACTIVATE = 0x08000000;

        readonly Dictionary<string, object> _contentByTabId = new();
        WinForms.NotifyIcon? _tray;

        AppState _state = new();
        bool _editMode = true;

        bool _draggingPanel;
        System.Windows.Point _dragStartPanel;
        double _startLeftPanel;
        double _startTopPanel;

        string StatePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "VisorView", "state.json");

        readonly Dictionary<string, double> _zoomByTabId = new();


        readonly HashSet<string> _adHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "doubleclick.net",
            "googleadservices.com",
            "googlesyndication.com",
            "googleads.g.doubleclick.net",
            "adservice.google.com",
            "pagead2.googlesyndication.com",
            "tpc.googlesyndication.com",
            "securepubads.g.doubleclick.net",
            "pubads.g.doubleclick.net",
            "adnxs.com",
            "criteo.com",
            "criteo.net",
            "rubiconproject.com",
            "openx.net",
            "taboola.com",
            "outbrain.com",
            "scorecardresearch.com",
            "quantserve.com",
            "adsrvr.org",
            "moatads.com"
        };


        public MainWindow()
        {
            InitializeComponent();
            _state = LoadState();

            


            Loaded += (_, __) =>
            {

                CloseButton.Click += (_, __) => Close();

                NewTabButton.Click += (_, __) =>
                {
                    AddTab(new TabState { ContentType = TabContentType.Browser, UrlOrQuery = "" }, true);
                    SaveState();
                };

                TabBarSettingsButton.Click += (_, __) => OpenSettings();


                ZoomInButton.Click += (_, __) => ZoomBy(+0.1);
                ZoomOutButton.Click += (_, __) => ZoomBy(-0.1);

                TabList.SelectionChanged += (_, __) =>
                {
                    _state.SelectedTabIndex = TabList.SelectedIndex;

                    if (TabList.SelectedItem is TabState ts)
                    {
                        ShowTab(ts);
                        SyncHeaderFromTab(ts);
                    }
                    UpdateUiForSelectedTab();
                    SaveState();
                };


                ContentTypeCombo.SelectionChanged += (_, __) =>
                {
                    if (TabList.SelectedItem is TabState ts)
                    {
                        ts.ContentType = ContentTypeCombo.SelectedIndex == 1 ? TabContentType.Browser : TabContentType.Image;
                        RebuildTabContent(ts);
                        SyncHeaderFromTab(ts);
                        UpdateUiForSelectedTab();
                        SaveState();
                    }
                };

                EnsureFolder();
                ExpandWindowToVirtualDesktop();
                EnsureFolder();

                Header.MouseLeftButtonDown += HeaderDown;
                Header.MouseMove += HeaderMove;
                Header.MouseLeftButtonUp += HeaderUp;

                BackButton.Click += BackButton_Click;
                ForwardButton.Click += ForwardButton_Click;

                SearchButton.Click += SearchButton_Click;
                SearchGoButton.Click += SearchGoButton_Click;
                SearchTextBox.KeyDown += SearchTextBox_KeyDown;


                WireResize();
                AttachHotkeys();
                CreateTray();

                ApplyStateToUI();
                ClampPanelToVisibleArea();
                ApplyMode(_state.StartInEditMode);

                UpdateUiForSelectedTab();
            };

            Closed += (_, __) =>
            {
                try
                {
                    if (_tray != null)
                    {
                        _tray.Visible = false;
                        _tray.Dispose();
                    }
                }
                catch { }

                DetachHotkeys();
            };
        }

        bool IsAdUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

            var host = uri.Host ?? "";
            foreach (var h in _adHosts)
                if (host == h || host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase))
                    return true;

            var p = uri.AbsolutePath ?? "";
            if (p.Contains("/ads", StringComparison.OrdinalIgnoreCase)) return true;
            if (p.Contains("/ad/", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        void AttachAdBlocker(WebView2 wv)
        {
            if (!_state.BlockAds) return;
            if (wv.CoreWebView2 == null) return;

            wv.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            wv.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;
            wv.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
        }

        void DetachAdBlocker(WebView2 wv)
        {
            if (wv.CoreWebView2 == null) return;
            wv.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;
        }

        async void ZoomBy(double delta)
        {
            var ts = CurrentTab();
            if (ts == null || ts.ContentType != TabContentType.Browser) return;
            if (!_contentByTabId.TryGetValue(ts.Id, out var obj) || obj is not WebView2 wv) return;

            await EnsureWebViewReady(wv);
            if (wv.CoreWebView2 == null) return;

            if (!_zoomByTabId.TryGetValue(ts.Id, out var z)) z = 1.0;
            z = Math.Clamp(z + delta, 0.25, 5.0);
            _zoomByTabId[ts.Id] = z;

            try
            {
                var inv = z.ToString(CultureInfo.InvariantCulture);
                await wv.ExecuteScriptAsync(
                    $"(function(){{ document.documentElement.style.zoom='{inv}'; document.body.style.zoom='{inv}'; }})();"
                );
            }
            catch { }
        }




        void TabCloseRequested(object sender, TabCloseRequestedEventArgs e)
        {
            CloseTab(e.Tab);
            e.Handled = true;
        }


        void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!_state.BlockAds) return;

            var url = e.Request?.Uri ?? "";
            if (!IsAdUrl(url)) return;

            var env = (sender as CoreWebView2)?.Environment;
            if (env == null) return;

            e.Response = env.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain");
        }

        async System.Threading.Tasks.Task EnsureWebViewReady(WebView2 wv)
        {
            await wv.EnsureCoreWebView2Async();
            if (wv.CoreWebView2 == null) return;

            if (_state.BlockAds) AttachAdBlocker(wv);
            else DetachAdBlocker(wv);
        }

        async void NavigateTab(TabState ts, string input)
        {
            if (!_contentByTabId.TryGetValue(ts.Id, out var content)) return;
            if (content is not WebView2 wv) return;

            await EnsureWebViewReady(wv);

            var url = BuildUrlFromInput(input);
            try { wv.CoreWebView2.Navigate(url); } catch { }
        }


        void EnsureFolder()
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }

        void CloseTabInline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Button b && b.CommandParameter is TabState ts)
                CloseTab(ts);
        }

        void RebuildTabContent(TabState ts)
        {
            if (_contentByTabId.TryGetValue(ts.Id, out var existing) && existing is WebView2 wv)
            {
                try { wv.Dispose(); } catch { }
            }

            _contentByTabId.Remove(ts.Id);

            if (TabList.SelectedItem == ts)
                ShowTab(ts);
        }

        void ApplyStateToUI()
        {
            Topmost = _state.AlwaysOnTop;
            Visibility = _state.WindowVisible ? Visibility.Visible : Visibility.Hidden;

            if (_state.RememberLayout)
            {
                Canvas.SetLeft(Panel, _state.PanelLeft);
                Canvas.SetTop(Panel, _state.PanelTop);
                Panel.Width = _state.PanelWidth;
                Panel.Height = _state.PanelHeight;
            }

            TabList.Items.Clear();
            _contentByTabId.Clear();

            if (_state.Tabs == null) _state.Tabs = new List<TabState>();
            if (_state.Tabs.Count == 0)
                _state.Tabs.Add(new TabState { ContentType = TabContentType.Browser, UrlOrQuery = "" });

            foreach (var t in _state.Tabs)
                AddTab(t, false);

            RenumberTabs();

            var idx = _state.SelectedTabIndex;
            if (idx < 0 || idx >= TabList.Items.Count) idx = 0;
            TabList.SelectedIndex = idx;

            var ct = CurrentTab();
            if (ct == null)
            {
                ShowEmptyState();
                return;
            }

            ShowTab(ct);
            SyncHeaderFromTab(ct);
            UpdateUiForSelectedTab();

            HintText.Visibility = Visibility.Hidden;
        }


        TabState? CurrentTab()
        {
            return TabList.SelectedItem as TabState;
        }


        void ApplyMode(bool edit)
        {
            _editMode = edit;
            ModeText.Text = edit ? "EDIT MODE" : "NORMAL MODE";


            BR.IsEnabled = edit;
            BR.Visibility = edit ? Visibility.Visible : Visibility.Hidden;

            SetClickThrough(_state.ClickThroughInNormalMode && !_editMode);
        }

        void SetClickThrough(bool on)
        {
            var h = new WindowInteropHelper(this).Handle;
            var s = GetWindowLong(h, GWL_EXSTYLE);

            if (on)
                s |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            else
                s &= ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

            SetWindowLong(h, GWL_EXSTYLE, s | WS_EX_TOOLWINDOW);
        }

        void WireResize()
        {
            BR.DragDelta += (_, e) =>
            {
                if (!_editMode) return;

                var ts = CurrentTab();
                var w = 0.0;
                var h = 0.0;

                if (ts != null && ts.ContentType == TabContentType.Image)
                {
                    w = Math.Max(300, Panel.Width + e.HorizontalChange);
                    h = Math.Max(240, Panel.Height + e.VerticalChange);
                }
                else if (ts != null && ts.ContentType == TabContentType.Browser)
                {
                    w = Math.Max(380, Panel.Width + e.HorizontalChange);
                    h = Math.Max(240, Panel.Height + e.VerticalChange);
                }

                Panel.Width = w;
                Panel.Height = h;

                if (_state.RememberLayout)
                {
                    _state.PanelWidth = w;
                    _state.PanelHeight = h;
                }

                SaveState();
            };
        }


        void RenumberTabs()
        {
        }


        void AddTab(TabState ts, bool select)
        {
            if (string.IsNullOrWhiteSpace(ts.Id))
                ts.Id = Guid.NewGuid().ToString("N");

            TabList.Items.Add(ts);
            RenumberTabs();

            if (select)
                TabList.SelectedItem = ts;

            HintText.Visibility = TabList.Items.Count == 0 ? Visibility.Visible : Visibility.Hidden;
        }

        void CloseTab(TabState ts)
        {
            if (_contentByTabId.TryGetValue(ts.Id, out var content) && content is WebView2 wv)
            {
                try { wv.Dispose(); } catch { }
            }

            _contentByTabId.Remove(ts.Id);

            var wasSelected = TabList.SelectedItem == ts;

            TabList.Items.Remove(ts);
            RenumberTabs();

            if (TabList.Items.Count == 0)
            {
                TabList.SelectedIndex = -1;
                _state.SelectedTabIndex = -1;
                ShowEmptyState();
                SaveState();
                return;
            }

            if (wasSelected && TabList.SelectedIndex < 0)
                TabList.SelectedIndex = 0;

            if (TabList.SelectedItem is TabState selected)
            {
                ShowTab(selected);
                SyncHeaderFromTab(selected);
            }

            UpdateUiForSelectedTab();
            SaveState();
        }

        void ShowEmptyState()
        {
            ContentHost.Content = null;
            HintText.Visibility = Visibility.Visible;
            ContentTypeCombo.SelectedIndex = 1;
            SearchTextBox.Text = "";
            UpdateUiForSelectedTab();
        }

        void ShowTab(TabState ts)
        {
            HintText.Visibility = Visibility.Hidden;

            if (!_contentByTabId.TryGetValue(ts.Id, out var content))
            {
                content = BuildContent(ts);
                _contentByTabId[ts.Id] = content;
            }

            ContentHost.Content = content as UIElement;

            if (ts.ContentType == TabContentType.Browser &&
                _contentByTabId.TryGetValue(ts.Id, out var obj2) &&
                obj2 is WebView2 wv2 &&
                _zoomByTabId.TryGetValue(ts.Id, out var z2))
            {
                _ = EnsureWebViewReady(wv2).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            var inv = z2.ToString(CultureInfo.InvariantCulture);
                            await wv2.ExecuteScriptAsync(
                                $"(function(){{ document.documentElement.style.zoom='{inv}'; document.body.style.zoom='{inv}'; }})();"
                            );
                        }
                        catch { }
                    });
                });
            }


            UpdateNavButtons();
        }

        object BuildContent(TabState ts)
        {
            if (ts.ContentType == TabContentType.Image)
            {
                var img = new Wpf.Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                if (!string.IsNullOrWhiteSpace(ts.ImagePath) && File.Exists(ts.ImagePath))
                    img.Source = LoadBitmap(ts.ImagePath);
                return img;
            }

            var wv = new WebView2();
            wv.NavigationCompleted += (_, __) => UpdateNavButtons();

            _ = EnsureWebViewReady(wv).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    var url = BuildUrlFromInput(ts.UrlOrQuery ?? "");
                    try { wv.CoreWebView2?.Navigate(url); } catch { }
                });
            });

            return wv;

        }

        static BitmapImage LoadBitmap(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            return bmp;
        }

        async System.Threading.Tasks.Task InitAndNavigate(WebView2 wv, string input)
        {
            await wv.EnsureCoreWebView2Async();
            if (wv.CoreWebView2 == null) return;

            var url = BuildUrlFromInput(input);
            try { wv.CoreWebView2.Navigate(url); } catch { }
        }

        string BuildUrlFromInput(string input)
        {
            input = (input ?? "").Trim();
            if (input.Length == 0) return SearchUrl("");

            if (Uri.TryCreate(input, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
                return u.ToString();

            if (input.Contains(".") && !input.Contains(" "))
                return "https://" + input;

            return SearchUrl(input);
        }

        string SearchUrl(string q)
        {
            var enc = Uri.EscapeDataString(q ?? "");
            return _state.DefaultSearchEngine switch
            {
                SearchEngine.DuckDuckGo => $"https://duckduckgo.com/?q={enc}",
                SearchEngine.Bing => $"https://www.bing.com/search?q={enc}",
                SearchEngine.Brave => $"https://search.brave.com/search?q={enc}",
                _ => $"https://www.google.com/search?q={enc}"
            };
        }

        void SyncHeaderFromTab(TabState ts)
        {
            ContentTypeCombo.SelectedIndex = ts.ContentType == TabContentType.Browser ? 1 : 0;

            if (ts.ContentType == TabContentType.Browser)
                SearchTextBox.Text = ts.UrlOrQuery ?? "";
            else
                SearchTextBox.Text = "";

            var minW = ts.ContentType == TabContentType.Browser ? 380 : 300;
            if (Panel.Width < minW)
            {
                Panel.Width = minW;

                if (_state.RememberLayout)
                    _state.PanelWidth = minW;

                SaveState();
            }
        }



        void UpdateUiForSelectedTab()
        {
            var ts = CurrentTab();
            if (ts == null)
            {
                HintText.Visibility = Visibility.Visible;
                SearchButton.Visibility = Visibility.Collapsed;
                UploadButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                ForwardButton.Visibility = Visibility.Collapsed;
                return;
            }

            var isBrowser = ts.ContentType == TabContentType.Browser;

            ZoomDock.Visibility = isBrowser ? Visibility.Visible : Visibility.Collapsed;

            SearchButton.Visibility = isBrowser ? Visibility.Visible : Visibility.Collapsed;
            UploadButton.Visibility = isBrowser ? Visibility.Collapsed : Visibility.Visible;

            BackButton.Visibility = isBrowser ? Visibility.Visible : Visibility.Collapsed;
            ForwardButton.Visibility = isBrowser ? Visibility.Visible : Visibility.Collapsed;

            UpdateNavButtons();
        }

        void UpdateNavButtons()
        {
            var ts = CurrentTab();
            if (ts == null || ts.ContentType != TabContentType.Browser)
            {
                BackButton.IsEnabled = false;
                ForwardButton.IsEnabled = false;
                return;
            }

            if (_contentByTabId.TryGetValue(ts.Id, out var content) && content is WebView2 wv && wv.CoreWebView2 != null)
            {
                BackButton.IsEnabled = wv.CoreWebView2.CanGoBack;
                ForwardButton.IsEnabled = wv.CoreWebView2.CanGoForward;
            }
            else
            {
                BackButton.IsEnabled = false;
                ForwardButton.IsEnabled = false;
            }
        }

        void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchPopup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }));
        }


        void SearchGoButton_Click(object sender, RoutedEventArgs e)
        {
            var ts = CurrentTab();
            if (ts == null) return;

            var q = (SearchTextBox.Text ?? "").Trim();
            ts.UrlOrQuery = q;

            if (ts.ContentType == TabContentType.Browser)
                NavigateTab(ts, q);

            SearchPopup.IsOpen = false;
            SaveState();
        }


        void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SearchGoButton_Click(sender, e);
        }


        void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var ts = CurrentTab();
            if (ts == null) return;

            PickImageForTab(ts);
            SaveState();
        }

        void PickImageForTab(TabState ts)
        {
            var dlg = new WinForms.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*"
            };

            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                ts.ContentType = TabContentType.Image;
                ts.ImagePath = dlg.FileName;

                if (_contentByTabId.TryGetValue(ts.Id, out var existing) && existing is Wpf.Image img)
                    img.Source = LoadBitmap(ts.ImagePath);
                else
                {
                    _contentByTabId.Remove(ts.Id);

                }

                var ct = CurrentTab();
                if (ct == null) return;

                ShowTab(ct);
                SyncHeaderFromTab(ct);
                UpdateUiForSelectedTab();

            }
        }

        void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var ts = CurrentTab();
            if (ts == null) return;
            if (!_contentByTabId.TryGetValue(ts.Id, out var content)) return;
            if (content is not WebView2 wv) return;
            if (wv.CoreWebView2 == null) return;

            if (wv.CoreWebView2.CanGoBack) wv.CoreWebView2.GoBack();
            UpdateNavButtons();
        }

        void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            var ts = CurrentTab();
            if (ts == null) return;
            if (!_contentByTabId.TryGetValue(ts.Id, out var content)) return;
            if (content is not WebView2 wv) return;
            if (wv.CoreWebView2 == null) return;

            if (wv.CoreWebView2.CanGoForward) wv.CoreWebView2.GoForward();
            UpdateNavButtons();
        }

        void OpenSettings()
        {
            var dlg = new SettingsWindow(CloneState(_state)) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _state = dlg.Result;
                foreach (var kv in _contentByTabId)
                    if (kv.Value is WebView2 wv && wv.CoreWebView2 != null)
                    {
                        if (_state.BlockAds) AttachAdBlocker(wv);
                        else DetachAdBlocker(wv);
                    }

                Topmost = _state.AlwaysOnTop;
                ApplyMode(_editMode);
                UpdateUiForSelectedTab();
                SaveState();
            }
        }

        static AppState CloneState(AppState s)
        {
            var json = JsonSerializer.Serialize(s);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }

        void SaveState()
        {
            _state.AlwaysOnTop = Topmost;
            _state.WindowVisible = Visibility == Visibility.Visible;

            var left = Canvas.GetLeft(Panel);
            var top = Canvas.GetTop(Panel);
            if (double.IsNaN(left)) left = 80;
            if (double.IsNaN(top)) top = 80;

            _state.PanelLeft = left;
            _state.PanelTop = top;
            _state.PanelWidth = Panel.Width;
            _state.PanelHeight = Panel.Height;

            _state.SelectedTabIndex = TabList.SelectedIndex;

            _state.Tabs = new List<TabState>();
            foreach (var item in TabList.Items)
                if (item is TabState ts)
                    _state.Tabs.Add(ts);

            EnsureFolder();
            File.WriteAllText(StatePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        }


        AppState LoadState()
        {
            try
            {
                if (File.Exists(StatePath))
                {
                    var s = JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath));
                    if (s != null)
                    {
                        if (s.Tabs == null) s.Tabs = new List<TabState>();
                        if (s.Tabs.Count == 0) s.Tabs.Add(new TabState { ContentType = TabContentType.Browser, UrlOrQuery = "" });
                        return s;
                    }
                }
            }
            catch { }

            var d = new AppState();
            d.Tabs.Add(new TabState { ContentType = TabContentType.Browser, UrlOrQuery = "" });
            return d;
        }


        void CreateTray()
        {
            _tray = new WinForms.NotifyIcon
            {
                Text = "VisorView",
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip();

            var showHide = new WinForms.ToolStripMenuItem("Show/Hide");
            showHide.Click += (_, __) => ToggleVisibility();
            menu.Items.Add(showHide);

            var newTab = new WinForms.ToolStripMenuItem("New Browser Tab");
            newTab.Click += (_, __) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AddTab(new TabState { ContentType = TabContentType.Browser, UrlOrQuery = "" }, true);
                    SaveState();
                });
            };
            menu.Items.Add(newTab);

            var exit = new WinForms.ToolStripMenuItem("Exit");
            exit.Click += (_, __) => Dispatcher.Invoke(Close);
            menu.Items.Add(exit);

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (_, __) => Dispatcher.Invoke(ToggleVisibility);
        }

        void ToggleVisibility()
        {
            Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }

        void ExpandWindowToVirtualDesktop()
        {
            WindowState = WindowState.Normal;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }


        void ClampPanelToVisibleArea()
        {
            var leftBound = 0.0;
            var topBound = 0.0;
            var rightBound = SystemParameters.VirtualScreenWidth - Panel.Width;
            var bottomBound = SystemParameters.VirtualScreenHeight - Panel.Height;

            var left = Canvas.GetLeft(Panel);
            var top = Canvas.GetTop(Panel);

            if (double.IsNaN(left)) left = 80;
            if (double.IsNaN(top)) top = 80;

            left = Math.Clamp(left, leftBound, Math.Max(leftBound, rightBound));
            top = Math.Clamp(top, topBound, Math.Max(topBound, bottomBound));

            Canvas.SetLeft(Panel, left);
            Canvas.SetTop(Panel, top);

            _state.PanelLeft = left;
            _state.PanelTop = top;
        }


        void AttachHotkeys()
        {
            var h = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(h);
            src.AddHook(WndProc);

            RegisterHotKey(h, HOTKEY_ID_TOGGLE_VIS, MOD_CONTROL | MOD_SHIFT, (uint)KeyInterop.VirtualKeyFromKey(Key.O));
            RegisterHotKey(h, HOTKEY_ID_TOGGLE_MODE, MOD_CONTROL | MOD_SHIFT, (uint)KeyInterop.VirtualKeyFromKey(Key.E));
        }

        void DetachHotkeys()
        {
            var h = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(h, HOTKEY_ID_TOGGLE_VIS);
            UnregisterHotKey(h, HOTKEY_ID_TOGGLE_MODE);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (id == HOTKEY_ID_TOGGLE_VIS)
                {
                    ToggleVisibility();
                    handled = true;
                }
                else if (id == HOTKEY_ID_TOGGLE_MODE)
                {
                    ApplyMode(!_editMode);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        void HeaderDown(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_editMode) return;
            if (e.ClickCount == 2) return;

            var src = e.OriginalSource as DependencyObject;
            while (src != null)
            {
                if (src is Wpf.Button ||
                    src is Wpf.TextBox ||
                    src is Wpf.ComboBox ||
                    src is System.Windows.Controls.Primitives.ToggleButton)
                    return;

                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            }

            _draggingPanel = true;
            _dragStartPanel = e.GetPosition(RootCanvas);
            _startLeftPanel = Canvas.GetLeft(Panel);
            _startTopPanel = Canvas.GetTop(Panel);
            Header.CaptureMouse();
        }

        void HeaderMove(object s, System.Windows.Input.MouseEventArgs e)
        {
            if (!_draggingPanel) return;

            var p = e.GetPosition(RootCanvas);
            var newLeft = _startLeftPanel + (p.X - _dragStartPanel.X);
            var newTop = _startTopPanel + (p.Y - _dragStartPanel.Y);

            Canvas.SetLeft(Panel, newLeft);
            Canvas.SetTop(Panel, newTop);

            if (_state.RememberLayout)
            {
                _state.PanelLeft = newLeft;
                _state.PanelTop = newTop;
            }
        }

        void HeaderUp(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_draggingPanel) return;
            _draggingPanel = false;
            Header.ReleaseMouseCapture();
            SaveState();
        }

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
