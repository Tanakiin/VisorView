using System;
using System.Collections.Generic;

namespace VisorView
{
    public enum TabContentType { Image, Browser }
    public enum SearchEngine { Google, DuckDuckGo, Bing, Brave }

    public sealed class AppState
    {
        public double PanelLeft { get; set; } = 80;
        public double PanelTop { get; set; } = 80;
        public double PanelWidth { get; set; } = 420;
        public double PanelHeight { get; set; } = 260;

        public bool WindowVisible { get; set; } = true;
        public int SelectedTabIndex { get; set; } = 0;

        public SearchEngine DefaultSearchEngine { get; set; } = SearchEngine.Google;

        public bool RememberLayout { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = true;
        public bool BlockAds { get; set; } = true;
        public bool StartInEditMode { get; set; } = true;
        public bool ClickThroughInNormalMode { get; set; } = true;

        public List<TabState> Tabs { get; set; } = new();

        public void ResetToDefaults()
        {
            PanelLeft = 80;
            PanelTop = 80;
            PanelWidth = 420;
            PanelHeight = 260;

            WindowVisible = true;
            SelectedTabIndex = 0;

            DefaultSearchEngine = SearchEngine.Google;

            RememberLayout = true;
            AlwaysOnTop = true;
            BlockAds = true;
            StartInEditMode = true;
            ClickThroughInNormalMode = true;

            Tabs = new List<TabState> { new TabState() };
        }

    }

    public sealed class TabState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Tab";
        public TabContentType ContentType { get; set; } = TabContentType.Browser;

        public string UrlOrQuery { get; set; } = "https://example.com";
        public string ImagePath { get; set; } = "";
    }

}
