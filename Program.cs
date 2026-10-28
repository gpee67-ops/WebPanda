using System;
#nullable enable
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace WebPanda
{
    internal static class Program
    {
        [STAThread]
        static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new BrowserWindow()); }
    }

    public class BrowserWindow : Form
    {
        private readonly TabControl tabs = new TabControl();
        private readonly Panel toolbar = new Panel();
        private readonly Panel bookmarkBar = new Panel();
        private readonly TextBox address = new TextBox();
        private Button back, forward, reload, home, go, newTab, closeTab, addBookmark, bookmarks, personalize;
        private readonly string homePath = Path.Combine(AppContext.BaseDirectory, "webpanda.html");
        private readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebPanda");
        private string bookmarksPath = "";
        private string settingsPath = "";
        private string historyPath = "";
        private List<HistoryEntry> history = new List<HistoryEntry>();
        private List<Bookmark> savedBookmarks = new List<Bookmark>();
        private BrowserSettings settings = new BrowserSettings();
        private bool changingAddress;
        private readonly Color dark = Color.FromArgb(7, 19, 15), panel = Color.FromArgb(10, 30, 21), green = Color.FromArgb(31, 122, 77), light = Color.FromArgb(86, 214, 139), textColor = Color.White;

        public BrowserWindow()
        {
            Text = "WebPanda"; Width = 1280; Height = 820; MinimumSize = new Size(800, 550); BackColor = dark; ForeColor = textColor;
            try { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "webpanda.ico")); } catch { }
            Directory.CreateDirectory(dataDir); bookmarksPath = Path.Combine(dataDir, "bookmarks.json"); settingsPath = Path.Combine(dataDir, "settings.json"); historyPath = Path.Combine(dataDir, "history.json");
            LoadData(); BuildToolbar(); BuildBookmarksBar();
            tabs.Dock = DockStyle.Fill; tabs.Font = new Font("Segoe UI", 10); tabs.DrawMode = TabDrawMode.Normal; tabs.SelectedIndexChanged += (_, _) => UpdateAddress();
            Controls.Add(tabs); Controls.Add(bookmarkBar); Controls.Add(toolbar);
            ApplyTheme(); UpdateBookmarkBar();
            Shown += async (_, _) => await NewBrowserTab();
        }

        private void LoadData()
        {
            try { if (File.Exists(bookmarksPath)) savedBookmarks = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(bookmarksPath)) ?? new(); } catch { savedBookmarks = new(); }
            try { if (File.Exists(settingsPath)) settings = JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(settingsPath)) ?? new(); } catch { settings = new(); }
            try { if (File.Exists(historyPath)) history = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(historyPath)) ?? new(); } catch { history = new(); }
        }
        private void SaveData()
        {
            try { File.WriteAllText(bookmarksPath, JsonSerializer.Serialize(savedBookmarks, new JsonSerializerOptions { WriteIndented = true })); } catch { }
            try { File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); } catch { }
            try { File.WriteAllText(historyPath, JsonSerializer.Serialize(history.Take(2000).ToList(), new JsonSerializerOptions { WriteIndented = true })); } catch { }
        }
        private Button MakeButton(string label, int width, Action action)
        {
            var b = new Button { Text = label, Width = width, Height = 34, Margin = new Padding(3, 6, 3, 4), FlatStyle = FlatStyle.Flat, BackColor = green, ForeColor = textColor, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); return b;
        }
        private void BuildToolbar()
        {
            toolbar.Dock = DockStyle.Top; toolbar.Height = 48; toolbar.Padding = new Padding(5, 0, 5, 0); toolbar.BackColor = panel;
            var brand = new Label { Text = "🐼 WebPanda", Width = 105, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = light };
            back = MakeButton("←", 38, GoBack); forward = MakeButton("→", 38, GoForward); reload = MakeButton("⟳", 38, Reload); home = MakeButton("⌂", 38, NavigateHome); newTab = MakeButton("+ Tab", 62, async () => await NewBrowserTab()); closeTab = MakeButton("× Tab", 62, CloseCurrentTab); addBookmark = MakeButton("☆", 38, AddCurrentBookmark); bookmarks = MakeButton("★", 38, ShowBookmarksMenu); personalize = MakeButton("⚙", 38, ShowPersonalizeMenu); go = MakeButton("Go", 48, () => Navigate(address.Text));
            address.Font = new Font("Segoe UI", 11); address.BorderStyle = BorderStyle.FixedSingle; address.Width = 360; address.Height = 30; address.Margin = new Padding(3, 9, 3, 4); address.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Navigate(address.Text); e.SuppressKeyPress = true; } };
            toolbar.Controls.Add(personalize); toolbar.Controls.Add(bookmarks); toolbar.Controls.Add(addBookmark); toolbar.Controls.Add(go); toolbar.Controls.Add(address); toolbar.Controls.Add(home); toolbar.Controls.Add(reload); toolbar.Controls.Add(forward); toolbar.Controls.Add(back); toolbar.Controls.Add(closeTab); toolbar.Controls.Add(newTab); toolbar.Controls.Add(brand);
            toolbar.Resize += (_, _) => { address.Width = Math.Max(160, toolbar.ClientSize.Width - 690); };
        }
        private void BuildBookmarksBar() { bookmarkBar.Dock = DockStyle.Top; bookmarkBar.Height = 34; bookmarkBar.BackColor = Color.FromArgb(13, 33, 25); }
        private async System.Threading.Tasks.Task NewBrowserTab(string? url = null)
        {
            var page = new TabPage("New Tab") { BackColor = dark };
            var browser = new WebView2 { Dock = DockStyle.Fill };
            page.Controls.Add(browser); tabs.TabPages.Add(page); tabs.SelectedTab = page;
            try
            {
                var envOptions = new CoreWebView2EnvironmentOptions();
                if (!string.IsNullOrWhiteSpace(settings.ProxyServer)) envOptions.AdditionalBrowserArguments = "--proxy-server=\"" + settings.ProxyServer.Replace("\"", "") + "\"";
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataDir, "WebView2"), envOptions);
                await browser.EnsureCoreWebView2Async(environment);
                browser.CoreWebView2.DocumentTitleChanged += (_, _) => { if (page.IsDisposed) return; var title = browser.CoreWebView2.DocumentTitle; if (string.IsNullOrWhiteSpace(title)) title = "New Tab"; page.Text = title.Length > 22 ? title.Substring(0, 22) + "…" : title; };
                browser.CoreWebView2.NavigationStarting += (_, e) => { if (tabs.SelectedTab == page) { changingAddress = true; address.Text = e.Uri; changingAddress = false; } };
                browser.CoreWebView2.NavigationCompleted += (_, _) => {
                    if (browser.CoreWebView2.Source != null && (browser.CoreWebView2.Source.StartsWith("http://") || browser.CoreWebView2.Source.StartsWith("https://"))) {
                        history.Insert(0, new HistoryEntry { Title = browser.CoreWebView2.DocumentTitle ?? browser.CoreWebView2.Source, Url = browser.CoreWebView2.Source, Visited = DateTime.Now });
                        if (history.Count > 2000) history.RemoveRange(2000, history.Count - 2000); SaveData();
                    }
                    if (tabs.SelectedTab == page) UpdateAddress();
                };
                if (!string.IsNullOrWhiteSpace(url)) browser.CoreWebView2.Navigate(url); else NavigateHome(browser);
            }
            catch (Exception ex) { MessageBox.Show("WebPanda couldn't start its web engine. Install the Microsoft Edge WebView2 Runtime and try again.\n\n" + ex.Message, "WebPanda", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private WebView2? CurrentBrowser => tabs.SelectedTab?.Controls.OfType<WebView2>().FirstOrDefault();
        private void NavigateHome() { var b = CurrentBrowser; if (b != null) NavigateHome(b); }
        private void NavigateHome(WebView2 b)
        {
            if (File.Exists(homePath)) b.CoreWebView2?.Navigate(new Uri(homePath).AbsoluteUri);
            else b.CoreWebView2?.Navigate("https://www.google.com");
        }
        private void Navigate(string input)
        {
            var b = CurrentBrowser; if (b?.CoreWebView2 == null) return; input = input.Trim(); if (input.Length == 0) return;
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https")) b.CoreWebView2.Navigate(uri.ToString());
            else if (input.Contains('.') && !input.Contains(' ')) b.CoreWebView2.Navigate("https://" + input);
            else b.CoreWebView2.Navigate(SearchUrl(input));
        }
        private string SearchUrl(string query)
        {
            string encoded = Uri.EscapeDataString(query);
            return settings.SearchEngine switch
            {
                "Bing" => "https://www.bing.com/search?q=" + encoded,
                "DuckDuckGo" => "https://duckduckgo.com/?q=" + encoded,
                "Brave Search" => "https://search.brave.com/search?q=" + encoded,
                "Yahoo" => "https://search.yahoo.com/search?p=" + encoded,
                _ => "https://www.google.com/search?q=" + encoded
            };
        }
        private void GoBack() { if (CurrentBrowser?.CoreWebView2?.CanGoBack == true) CurrentBrowser.CoreWebView2.GoBack(); }
        private void GoForward() { if (CurrentBrowser?.CoreWebView2?.CanGoForward == true) CurrentBrowser.CoreWebView2.GoForward(); }
        private void Reload() { CurrentBrowser?.Reload(); }
        private void UpdateAddress() { changingAddress = true; address.Text = CurrentBrowser?.Source?.ToString() ?? ""; changingAddress = false; }
        private void CloseCurrentTab()
        {
            if (tabs.TabPages.Count <= 1) { NavigateHome(); return; }
            var selected = tabs.SelectedTab; if (selected != null) { tabs.TabPages.Remove(selected); selected.Dispose(); }
        }
        private void AddCurrentBookmark()
        {
            var b = CurrentBrowser; if (b?.Source == null) return; string url = b.Source.ToString(); string title = b.CoreWebView2?.DocumentTitle ?? url;
            if (savedBookmarks.Any(x => x.Url == url)) { MessageBox.Show("That page is already bookmarked.", "WebPanda Bookmarks"); return; }
            savedBookmarks.Add(new Bookmark { Title = string.IsNullOrWhiteSpace(title) ? url : title, Url = url }); SaveData(); UpdateBookmarkBar();
        }
        private void ShowBookmarksMenu()
        {
            var menu = new ContextMenuStrip(); menu.Items.Add("Bookmark this page", null, (_, _) => AddCurrentBookmark()); menu.Items.Add(new ToolStripSeparator());
            if (savedBookmarks.Count == 0) menu.Items.Add("No bookmarks yet")!.Enabled = false;
            foreach (var item in savedBookmarks.ToList()) menu.Items.Add(item.Title, null, (_, _) => Navigate(item.Url));
            if (savedBookmarks.Count > 0) { menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Manage bookmarks…", null, (_, _) => ManageBookmarks());
            menu.Items.Add("History…", null, (_, _) => ShowHistory()); }
            menu.Show(bookmarks, new Point(0, bookmarks.Height));
        }
        private void UpdateBookmarkBar()
        {
            bookmarkBar.Controls.Clear(); bookmarkBar.Visible = settings.ShowBookmarksBar; bookmarkBar.Height = settings.ShowBookmarksBar ? 34 : 0;
            if (!settings.ShowBookmarksBar) return;
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = bookmarkBar.BackColor, Padding = new Padding(4, 0, 4, 0) };
            foreach (var item in savedBookmarks.Take(12)) { var b = new Button { Text = "★ " + item.Title, AutoSize = true, Height = 27, Margin = new Padding(2), FlatStyle = FlatStyle.Flat, BackColor = green, ForeColor = textColor, Font = new Font("Segoe UI", 8) }; b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => Navigate(item.Url); flow.Controls.Add(b); }
            bookmarkBar.Controls.Add(flow);
        }
        private void ManageBookmarks()
        {
            using var form = new Form { Text = "Manage WebPanda Bookmarks", Width = 620, Height = 430, StartPosition = FormStartPosition.CenterParent, BackColor = dark, ForeColor = textColor };
            var list = new ListBox { Dock = DockStyle.Fill, BackColor = panel, ForeColor = textColor, Font = new Font("Segoe UI", 10) };
            void RefreshList() { list.Items.Clear(); foreach (var bm in savedBookmarks) list.Items.Add(bm.Title + " — " + bm.Url); }
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(6) };
            var open = MakeButton("Open", 80, () => { if (list.SelectedIndex >= 0) { Navigate(savedBookmarks[list.SelectedIndex].Url); form.Close(); } });
            var remove = MakeButton("Remove", 80, () => { if (list.SelectedIndex >= 0) { savedBookmarks.RemoveAt(list.SelectedIndex); SaveData(); RefreshList(); UpdateBookmarkBar(); } });
            var done = MakeButton("Done", 80, () => form.Close()); buttons.Controls.Add(open); buttons.Controls.Add(remove); buttons.Controls.Add(done); form.Controls.Add(list); form.Controls.Add(buttons); RefreshList(); form.ShowDialog(this);
        }
        private void ShowPersonalizeMenu()
        {
            var menu = new ContextMenuStrip(); var themes = new ToolStripMenuItem("Theme");
            foreach (var name in new[] { "Forest Green", "Ocean Blue", "Midnight Purple", "Rose Pink", "Classic Dark" }) { var n = name; themes.DropDownItems.Add(n, null, (_, _) => { settings.Theme = n; SaveData(); ApplyTheme(); }); }
            menu.Items.Add(themes);
            var search = new ToolStripMenuItem("Default search engine");
            foreach (var name in new[] { "Google", "Bing", "DuckDuckGo", "Brave Search", "Yahoo" })
            {
                var engine = name;
                var choice = new ToolStripMenuItem(engine) { Checked = settings.SearchEngine == engine, CheckOnClick = false };
                choice.Click += (_, _) => { settings.SearchEngine = engine; SaveData(); ShowPersonalizeMenu(); };
                search.DropDownItems.Add(choice);
            }
            menu.Items.Add(search);
            var bar = new ToolStripMenuItem(settings.ShowBookmarksBar ? "Hide bookmarks bar" : "Show bookmarks bar", null, (_, _) => { settings.ShowBookmarksBar = !settings.ShowBookmarksBar; SaveData(); UpdateBookmarkBar(); }); menu.Items.Add(bar);
            menu.Items.Add("Manage bookmarks…", null, (_, _) => ManageBookmarks());
            menu.Items.Add("History…", null, (_, _) => ShowHistory()); menu.Items.Add("WebPanda settings folder", null, (_, _) => { try { System.Diagnostics.Process.Start("explorer.exe", dataDir); } catch { } }); menu.Show(personalize, new Point(0, personalize.Height));
        }
        private void ShowHistory()
        {
            using var form = new Form { Text = "WebPanda History", Width = 800, Height = 520, StartPosition = FormStartPosition.CenterParent, BackColor = dark, ForeColor = textColor };
            var search = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Search history…", Font = new Font("Segoe UI", 10) };
            var list = new ListBox { Dock = DockStyle.Fill, BackColor = panel, ForeColor = textColor, Font = new Font("Segoe UI", 9) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
            List<HistoryEntry> shown = new();
            void RefreshList() { shown = history.Where(h => (h.Title + " " + h.Url).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList(); list.Items.Clear(); foreach (var h in shown) list.Items.Add($"{h.Visited:g}  |  {h.Title}  |  {h.Url}"); }
            search.TextChanged += (_, _) => RefreshList();
            buttons.Controls.Add(MakeButton("Open", 85, () => { if (list.SelectedIndex >= 0) { Navigate(shown[list.SelectedIndex].Url); form.Close(); } }));
            buttons.Controls.Add(MakeButton("Clear history", 110, () => { if (MessageBox.Show("Clear all WebPanda browsing history?", "WebPanda", MessageBoxButtons.YesNo) == DialogResult.Yes) { history.Clear(); SaveData(); RefreshList(); } }));
            buttons.Controls.Add(MakeButton("Close", 85, () => form.Close())); form.Controls.Add(list); form.Controls.Add(search); form.Controls.Add(buttons); RefreshList(); form.ShowDialog(this);
        }
        private void ShowPrivacySettings()
        {
            using var form = new Form { Text = "WebPanda Privacy: Proxy & VPN", Width = 570, Height = 330, StartPosition = FormStartPosition.CenterParent, BackColor = dark, ForeColor = textColor };
            var info = new Label { Text = "PROXY\r\nEnter a proxy server such as http://127.0.0.1:8080. It applies after restarting WebPanda.\r\n\r\nVPN\r\nWebPanda does not include a VPN server or subscription. To use a VPN, connect to a trusted VPN provider's Windows app; it can protect traffic from other apps too. A proxy is not a VPN and does not encrypt all device traffic.", Dock = DockStyle.Top, Height = 160, Padding = new Padding(12), ForeColor = textColor };
            var proxy = new TextBox { Text = settings.ProxyServer, PlaceholderText = "Proxy address (leave blank to disable)", Dock = DockStyle.Top, Margin = new Padding(12) };
            var save = MakeButton("Save proxy setting", 150, () => { settings.ProxyServer = proxy.Text.Trim(); SaveData(); MessageBox.Show("Proxy setting saved. Restart WebPanda for it to take effect.", "WebPanda"); form.Close(); });
            var close = MakeButton("Close", 80, () => form.Close()); var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 }; buttons.Controls.Add(save); buttons.Controls.Add(close); form.Controls.Add(proxy); form.Controls.Add(info); form.Controls.Add(buttons); form.ShowDialog(this);
        }
        private void ApplyTheme()
        {
            Color bg = dark, p = panel, accent = light, buttonColor = green;
            switch (settings.Theme) { case "Ocean Blue": bg = Color.FromArgb(7, 16, 28); p = Color.FromArgb(12, 29, 49); accent = Color.FromArgb(100, 190, 255); buttonColor = Color.FromArgb(30, 95, 155); break; case "Midnight Purple": bg = Color.FromArgb(18, 10, 27); p = Color.FromArgb(33, 18, 47); accent = Color.FromArgb(200, 145, 255); buttonColor = Color.FromArgb(104, 53, 145); break; case "Rose Pink": bg = Color.FromArgb(28, 10, 18); p = Color.FromArgb(47, 18, 31); accent = Color.FromArgb(255, 145, 185); buttonColor = Color.FromArgb(150, 45, 88); break; case "Classic Dark": bg = Color.FromArgb(24, 24, 24); p = Color.FromArgb(40, 40, 40); accent = Color.FromArgb(230, 230, 230); buttonColor = Color.FromArgb(65, 65, 65); break; }
            BackColor = bg; toolbar.BackColor = p; bookmarkBar.BackColor = p; tabs.BackColor = bg; foreach (Control c in toolbar.Controls) { if (c is Button b) { b.BackColor = buttonColor; b.ForeColor = Color.White; } if (c is Label l) l.ForeColor = accent; } address.BackColor = Color.FromArgb(Math.Min(255, p.R + 12), Math.Min(255, p.G + 12), Math.Min(255, p.B + 12)); address.ForeColor = Color.White; UpdateBookmarkBar();
        }
        protected override void OnFormClosed(FormClosedEventArgs e) { SaveData(); base.OnFormClosed(e); }
        private class Bookmark { public string Title { get; set; } = "Bookmark"; public string Url { get; set; } = "https://www.google.com"; }
        private class BrowserSettings { public string Theme { get; set; } = "Ocean Blue"; public bool ShowBookmarksBar { get; set; } = true; public string SearchEngine { get; set; } = "Google"; public string ProxyServer { get; set; } = ""; }
        private class HistoryEntry { public string Title { get; set; } = ""; public string Url { get; set; } = ""; public DateTime Visited { get; set; } }
    }
}
