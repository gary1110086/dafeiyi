# DaFeiYi · 大肥译 🐟

[简体中文](README.md) | **English**

A floating translation assistant for Windows, with a little whale companion to keep you company while you read papers, look up terms, and understand equations. Select some text and click the small button to translate or explain it, switch to automatic or clipboard translation, or simply let her stay on your desktop.

Choose Simplified Chinese or English controls in Settings → Reading (restart after saving). Choose Chinese, English or Japanese answers independently.

![DaFeiYi's reading popup and whale companion on an example desktop](docs/images/desktop-companion.png)

## Meet your desktop companion

She can sit quietly on your desktop, enjoy a head pat, or have a little token snack. Just want some company? Right-click her and choose **仅陪伴 · 关闭划词** (Companion only · Disable selection translation).

![The whale companion idling, enjoying a head pat, and eating tokens](docs/images/pet-interactions.png)

Drag her to the left or right edge of the screen and she will tuck herself away, leaving her head peeking out. Move your mouse closer to bring her back with a continuous animation; move away and she will tuck herself away again.

![Continuous animation of peeking, revealing, and tucking away at the right screen edge](docs/images/edge-peek.gif)

These previews combine this version's character sprites, edge animation logic, and a popup screenshot with a clean example desktop. They contain no personal desktop or account information. Companion interactions run locally, without model requests or API usage.

<details>
<summary>A closer look at the translation popup</summary>

Both website-account and API responses appear in the same floating window, with support for Markdown tables and mathematical notation.

![A DeepSeek website-account response rendered in DaFeiYi's floating window](docs/images/preview.png)

</details>

## Download and get started

1. Open the [latest release](https://github.com/gary1110086/dafeiyi/releases/latest), download **DaFeiYi-Windows-x64-v1.1.0.zip**, and extract the entire archive into a folder.
2. Double-click **轻译.exe** to launch the app. To add a desktop shortcut, run **安装到桌面.cmd**. To also enable startup at Windows sign-in, run **安装并开机启动.cmd**.
3. In Settings, choose your connection: **官网账号** (Website account) to sign in to your own DeepSeek account and return to the popup, or **API** to enter your own API key.
4. Select a passage and click **翻译** (Translate) or **解释** (Explain). Both website and API responses stream into the same whale-themed popup.

Supports Windows 10/11 x64 with .NET Framework 4.8. Website-account mode also requires the [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), which is commonly present on recent Windows/Edge installations. No Python, Node.js, or Electron installation is required.

## New in v1.1

- Website composer controls are located by visible inputs and Chinese/English semantics; ordinary translation no longer requires a thinking toggle.
- The sign-in action selects website mode immediately. Sign in inside the app, then click **Done signing in · return**. **Check website** inspects readiness without sending a question.
- Failures keep your source, current crop and follow-up question. Use **Check website / sign in** and **Retry this request**. Unsent website text or attachments are preserved, with no automatic API fallback.
- **Ctrl+Alt+S → Image / formula**: crop, preview, then explain a formula, analyze a chart, or read and translate. Images are understood visually, beyond text OCR. Unclear or cropped information can still cause errors.
- API images use **deepseek-flash** by default. Website images use your account's upload control. Capabilities, limits and charges depend on the service.
- Separate interface and answer languages, a local first-run selection demo, and a latest-release download link in Settings.
- Run the new release's installer to update; user credentials, login, history and saved terms remain in the user profile.

## Features

- Four modes: click to translate, automatic translation, clipboard translation, and companion only. The selection button has its own drag handle.
- DeepSeek website-account and API connections, with quick-response and deep-thinking options.
- Local rendering of Markdown headings, lists, code, tables, and LaTeX equations.
- A draggable, resizable popup that can be pinned. When unpinned, clicking another app dismisses it; closing it while a response is generating cancels the request.
- Follow-up questions about the current passage, the latest 30 completed records, saved terms, personal notes, and preferred translations.
- Two built-in backgrounds, custom backgrounds, and adjustable font size and line spacing.
- Head pats, tail movements, token snacks, dragging, and animated peeking at either screen edge. These interactions do not send model requests.

| Shortcut | Action |
| --- | --- |
| Ctrl + Alt + D | Read the selection; try copying it if direct selection access is unavailable |
| Ctrl + Alt + V | Translate text already copied to the clipboard |
| Ctrl + Alt + S | Text OCR, or crop formulas, charts and images |
| Esc | Dismiss the result |
| Ctrl + E | Switch between compact and expanded views |

Selection detection depends on whether the source app exposes its selected text. If it does not, use copying or screen OCR. OCR output can be edited; equations and text with multiple columns may not be recognized accurately.

## Website troubleshooting

1. Select **Website account** and click **Use website / sign in**. Complete sign-in inside the app; external browser sessions are separate.
2. Complete any verification and click **Done signing in · return**. Settings shows whether the page is ready.
3. Click **Retry this request** in the popup. If a website draft exists, handle it in the account window first.
4. If it still fails, use **Export diagnostics** and include the displayed error. No password, cookies or API key are needed.

## Privacy and connections

**The release contains no developer API key, login session, personal settings, history, saved terms, or custom backgrounds. Everyone uses their own account.**

- Personal data stays locally under `%LOCALAPPDATA%/LightTranslate/`. API keys, history, and saved terms are encrypted using the current Windows user's credentials.
- Website sign-in uses a separate WebView2 profile. The app does not read your account password or export cookies; you complete any verification on the website yourself.
- Text OCR runs locally. Image understanding sends only the crop after you confirm; full-screen images are never uploaded. Image follow-ups resend the current crop and conversation context. The crop stays in session memory; saved history contains its label and answer, without image bytes.
- Exported connection diagnostics contain only status and WebView2 version, without accounts, cookies, keys, source text or chats.
- Automatic and clipboard modes send the corresponding text for processing. Choose a mode that fits your workflow; API charges belong to your own account.
- Website-account mode works through a web session and may need updates when the website changes. A failure does not automatically switch to the paid API.

This is an unofficial project with no affiliation with DeepSeek.

## Build and verify

After downloading the source, run the following in its directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The build uses the .NET Framework C# compiler (`csc`) provided by Windows. Dependencies, fonts, sprite sheets, and licenses are included.

Run `verify.ps1` for local verification. These checks use a local mock service and do not need a real API key. Tests briefly create windows and move the mouse.

Real image connection tests are explicit: `--vision-connect-test <output-directory> web|api`. They send a synthetic shape image; the API test has a small usage charge and is excluded from regular verification.

The real website-account test must be invoked explicitly with `--web-connect-test <output-directory>`. It sends short test questions through your own website account and is not run automatically by the regular verification script.

## License

The code is licensed under [MIT](LICENSE). Third-party animations, fonts, and DLLs retain their own licenses; see the [third-party notices](THIRD_PARTY_NOTICES.md). Built-in backgrounds are shared with this release and do not thereby become covered by the code's MIT license. Character and trademark rights remain with their respective owners.

## Animation credits and acknowledgments

DaFeiYi's desktop whale animations were inspired by **DeepSeek Harness's dafeiyu desktop-pet plugin** and use the [dsh-pet](https://github.com/PC2005-cloud/dsh-pet) animation assets adopted by that plugin. We adapted them for desktop companionship, translation status feedback, head pats and token snacks, and peeking at either screen edge.

Thank you to the original project authors and asset creators for bringing this lovely whale to more people's reading and learning. The assets retain their original copyrights and licenses; see the [visual asset notice](Assets/Whale/ASSET_LICENSE.md). This is an independently developed, unofficial tool with no official affiliation or partnership with DeepSeek, DeepSeek Harness, or the related original projects.
