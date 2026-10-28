WEBPANDA — GITHUB BUILD KIT

Features: tabs, Google search/address bar, bookmarks and bookmarks bar, themes, saved preferences, browsing history with search and clear controls, configurable HTTP/HTTPS proxy, custom WebPanda app icon, and branded start page.

IMPORTANT PRIVACY NOTE: WebPanda does not provide its own VPN service or VPN servers. Connect to a trusted VPN provider's Windows app for actual VPN protection. The proxy setting is browser-specific, applies after restart, and is not a VPN. Do not enter proxy credentials into untrusted servers.

BUILD WITH GITHUB (no local .NET install required):
1. Download and extract this ZIP.
2. Open your existing GitHub repository for PandaBrowser/WebPanda.
3. Upload/replace the contents of the WebPanda_Windows folder in the repository root, including the hidden .github folder. If your repository is named PandaBrowser, it can keep that name; the built app will be WebPanda.
4. Commit the changes to the main branch.
5. Open the repository's Actions tab and wait for “Build WebPanda for Windows” to finish successfully.
6. Open the successful workflow run and download the WebPanda-Windows-x64 artifact. Extract it and run WebPanda.exe. Keep the accompanying files together.

The PC needs Microsoft Edge WebView2 Runtime. The workflow builds the app on GitHub's Windows runner; it does not require Python or a local .NET SDK.
