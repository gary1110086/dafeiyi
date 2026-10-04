# 本地 Markdown / 数学渲染依赖

全部 DLL 和字体已随包附带，正常运行与构建不下载组件，不安装浏览器、Node 或 Python。数学在本机排版，保留原始 Markdown/LaTeX 供复制，不加载远程图片或执行 HTML。

| NuGet 包 | 固定版本 | 许可 |
|---|---|---|
| Markdig | 1.4.0 | BSD-2-Clause |
| WpfMath / XamlMath.Shared | 2.1.0 | MIT；嵌入字体见 FONT-LICENSES.md |
| CSharpMath / CSharpMath.Editor / CSharpMath.Rendering / CSharpMath.SkiaSharp | 0.5.1 | MIT；嵌入字体见 CSharpMath-FONT-NOTICES.txt |
| SkiaSharp / SkiaSharp.NativeAssets.Win32 | 2.88.9 | MIT，第三方声明见 SkiaSharp-NOTICES.txt |
| System.Memory | 4.6.3 | MIT |
| System.Buffers / System.Numerics.Vectors | 4.6.1 | MIT |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | MIT |

依赖包从 NuGet 官方 v3-flatcontainer 下载；托管程序集分别取 net462 或 netstandard2.0，原生 Skia 只取 win-x64。`app.config` 显式绑定到附带的 SkiaSharp 2.88、Memory/Buffers 4.0.5、Numerics.Vectors 4.1.6、Unsafe 6.0.3 程序集版本；以实际渲染测试验证兼容性。

WpfMath 直接生成 WPF 矢量几何；遇到物理常用的粗体向量、分段函数等语法，CSharpMath 排版后经 Skia 生成仅含矢量路径的 SVG，再转换为 WPF 矢量绘制。SVG 不交给浏览器执行，也不保留临时图片。`boldsymbol` 在第二排版器中映射到同样只作用于单个参数的 `mathbfit` 粗斜体样式。

公式缓存最多 64 项；无法识别、未完整或过长的公式显示原文。这里支持常见数学 LaTeX，不是完整 TeX 文档编译器，未提供用户宏、外部文件或任意宏包加载。

版权原文：Markdig-LICENSE.txt、XamlMath-LICENSE.txt、CSharpMath-LICENSE.txt、Microsoft-LICENSE.txt、SkiaSharp-LICENSE.txt、SkiaSharp-NOTICES.txt。

嵌入数学字体保持上游原样：WpfMath 的 Computer Modern/AMS 字体许可在 FONT-LICENSES.md；CSharpMath 的 AMS 黑板粗体、Cyrillic Modern 字体使用其中完整 OFL-1.1 文本，具体版权与保留字体名见 CSharpMath-FONT-NOTICES.txt；Latin Modern Math 使用 GUST-FONT-LICENSE.txt。

上游项目：
- https://github.com/xoofx/markdig
- https://github.com/ForNeVeR/xaml-math
- https://github.com/verybadcat/CSharpMath
- https://github.com/mono/SkiaSharp
- https://github.com/dotnet/maintenance-packages

## 官网账号窗口依赖

Microsoft.Web.WebView2 **1.0.4258.31**，来自 NuGet 官方 flatcontainer；托管 DLL 取 net462，WebView2Loader.dll 取 win-x64。许可与声明见 WebView2-LICENSE.txt、WebView2-NOTICE.txt。官网窗口使用系统已安装的 Evergreen WebView2 Runtime；构建不下载组件。API 翻译与本地 Markdown/数学渲染不依赖启动官网浏览器。

官方集成文档：https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
