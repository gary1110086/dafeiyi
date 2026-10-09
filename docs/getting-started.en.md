# DaFeiYi · Quick start

[简体中文](getting-started.md) | **English** · [Back to README](../README.en.md)

## Download and install

Get the Windows-x64 ZIP from the [latest release](https://github.com/gary1110086/dafeiyi/releases/latest) and extract it fully. The Source archive is for developers.

Run **安装到桌面.cmd** for a desktop shortcut, or **安装并开机启动.cmd** to also launch at Windows sign-in. Install over the previous version without uninstalling: local settings, login, history, terms and backgrounds are preserved.

## Try first, then connect

Open **Settings → Getting started → Try selection demo**. Select an English sentence and click Translate. The local demo makes no model requests.

Choose a provider in **Connection & models**:

- **Website account**: sign in inside the app, complete verification, check readiness and return to the popup. Closing the account window preserves login. Chrome and other browsers have independent sessions.
- **API**: enter your own key, test the connection and save. Usage is billed to your API account. Website failures never switch to API automatically.

## Read text, formulas and images

Start in Button mode: select a passage and click Translate or Explain. Click the whale to switch to Automatic, Clipboard or Companion; hold and drag to move her.

If a selection is inaccessible, copy it and press **Ctrl+Alt+V**. Use **Ctrl+Alt+S** for screen text. For equations, charts or diagrams, choose Image / formula and confirm the crop before sending.

An unpinned result hides when you click another app; **Esc** hides it too. Pin to keep it visible, drag the edges to resize, or use **Aa** for typography. Save terms with notes and preferred translations.

## Troubleshooting

| Symptom | First checks |
|---|---|
| No selection button | Check pause and Companion mode; try copying or screen capture if the app does not expose selections |
| Website translation fails | Sign in inside the app; complete verification or resolve an unsent draft; check readiness and retry |
| Account window cannot open | Install the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/); use the browser helper to check your connection |
| Shortcut does not respond | Another app may use it; choose screen capture or clipboard translation from the whale menu |
| Updates fail after a repository rename | Install v1.1.2 or later manually once, then use Updates & about |

Still stuck? [Report an issue](https://github.com/gary1110086/dafeiyi/issues/new/choose) with the app version, provider, mode, steps and error message. The account window can export connection diagnostics without source text, account details, cookies or API keys.

**Do not submit keys, passwords, cookies, personal settings or unredacted private screenshots.** Automatic and Clipboard modes send the corresponding text. Switch to Button or Companion before copying private content.
