# 内核依赖闭包清单（自动生成，勿手改）

> 生成时间：2026-09-11 19:32:08 +08:00 ｜ 生成脚本：`shell/Services/tools/kernel-deps-closure.ps1`
> 权威依据：(1) `data/player/AIPlayer.MpvHost.deps.json`（内核发布依赖清单，55 个 library）
>            (2) `reversed/MpvHost/**/*.cs` + `reversed/ThirdParty/**/*.cs`（源码里的 `DllImport` / `NativeLibrary.Load` / `libmpv-2.dll` 字面量）
> 目标 RID：`.NETCoreApp,Version=v9.0/win-x64`

## 0. 为什么需要这份清单（一句话机制）

**.NET 默认装载器只对登记在 `<App>.deps.json` 里的程序集探测应用目录。**
⇒ post-build 复制（或手工拷贝）进来的托管 DLL **探测不到**，症状是「文件明明在输出目录，却抛
`FileNotFoundException` / stowed exception `0xC000027B`（无事件日志、无 WER）」。
⇒ 两条正解：**(1)** 走 `PackageReference`（NuGet 自动写 deps.json）；**(2)** 手工 `Reference` + 显式
`AppDomain.AssemblyResolve` / `AssemblyLoadContext.Resolving` 从句 `AppContext.BaseDirectory` 解析。
⇒ 而 **native** 库（`libmpv-2.dll` 等）**不经过这套机制**，必须实打实放在输出目录里 —— 见 §4。

## 1. 分类统计

| 类别 | 含义 | 库数 | 文件数 | 是否需手工复制 |
|---|---|---|---|---|
| `BclRuntime` | .NET 运行时（BCL；本清单声明 9.0.14） | 1 | 168 | 否 —— 由 RollForward 或自包含发布提供 |
| `LocalReference` | 本地程序集引用（`<Reference>` + HintPath，如 Danmaku / Richasy 发布版） | 5 | 5 | 是 —— NuGet 无法还原 |
| `ManagedDependency` | 内核的托管依赖（NuGet 包 DLL） | 40 | 41 | 否 —— 由 PackageReference / Resolving 解决 |
| `WinAppSdk` | Windows App SDK 受管组件 | 6 | 29 | 否 —— 由 `WindowsAppSDKSelfContained=true` 自动复制 |
| `WinSdkRef` | Windows SDK 投影引用包（编译期） | 1 | 2 | 否 —— 运行期不需要 |

托管面合计 **245** 条 runtime 记录；去重后需能被解析的程序集名 **43** 个。
native 面：deps.json 声明 **20** 条；内核源码动态加载名 **10** 个（其中在 data/player 落地 **2** 个）。

## 2. 本地程序集引用（必须随输出目录存在）

| 文件 | 在 data/player 存在 |
|---|---|
| `Danmaku.Legacy.dll` | ✅ |
| `Danmaku.Share.dll` | ✅ |
| `Richasy.MpvKernel.Core.dll` | ✅ |
| `Richasy.MpvKernel.dll` | ✅ |
| `Richasy.MpvKernel.WinUI.dll` | ✅ |

## 3. 托管依赖（内核 NuGet 面：逐个都需能被解析）

```
AIPlayer.MpvHost.dll
CommunityToolkit.Common.dll
CommunityToolkit.HighPerformance.dll
CommunityToolkit.Mvvm.dll
CommunityToolkit.WinUI.Animations.dll
CommunityToolkit.WinUI.Controls.Sizers.dll
CommunityToolkit.WinUI.Extensions.dll
CommunityToolkit.WinUI.Helpers.dll
CommunityToolkit.WinUI.Media.dll
CommunityToolkit.WinUI.Triggers.dll
FluentIcons.Common.dll
FluentIcons.WinUI.dll
FluentResults.dll
Microsoft.Extensions.Configuration.Abstractions.dll
Microsoft.Extensions.DependencyInjection.Abstractions.dll
Microsoft.Extensions.DependencyInjection.dll
Microsoft.Extensions.Diagnostics.Abstractions.dll
Microsoft.Extensions.FileProviders.Abstractions.dll
Microsoft.Extensions.Hosting.Abstractions.dll
Microsoft.Extensions.Logging.Abstractions.dll
Microsoft.Extensions.Logging.dll
Microsoft.Extensions.Options.dll
Microsoft.Extensions.Primitives.dll
Microsoft.Graphics.Canvas.Interop.dll
Microsoft.Web.WebView2.Core.Projection.dll
Microsoft.Win32.SystemEvents.dll
Microsoft.Windows.AI.MachineLearning.Projection.dll
Richasy.MpvKernel.Core.dll
Richasy.MpvKernel.dll
Richasy.MpvKernel.WinUI.dll
Richasy.WinUIKernel.Share.dll
RichasyKernel.Abstractions.dll
RichasyKernel.Core.dll
Serilog.dll
Serilog.Extensions.Logging.dll
Serilog.Sinks.File.dll
System.Diagnostics.DiagnosticSource.dll
System.Drawing.Common.dll
System.Private.Windows.Core.dll
Win32.NativeWindow.dll
WinUIEx.dll
```

> ui 的实测教训：**只抄 `Richasy*` / `CommunityToolkit*` 不够** —— 依次还缺 `FluentIcons.WinUI`、
> `FluentIcons.Common`、`WinUIEx`（本表逐项列出了全部 41 个，照表核对即可）。

## 4. native 面

### 4.1 内核源码里**运行期动态加载**的 native（deps.json 不登记，必须手工放）

| 名称 | 在 data/player | 出现处（内核源码文件） | 结论 |
|---|---|---|---|
| `api-ms-win-shcore-scaling-l1-1-1.dll` | ❌ 不在 data/player | `PInvoke.cs` | 系统 DLL（由 OS 提供，无需随包） |
| `comctl32.dll` | ❌ 不在 data/player | `PlayerViewModel.cs` | 系统 DLL（由 OS 提供，无需随包） |
| `dwmapi.dll` | ❌ 不在 data/player | `MpvPlayerWindow.cs` | 系统 DLL（由 OS 提供，无需随包） |
| `gdi32.dll` | ❌ 不在 data/player | `MpvPlayerWindow.cs` | 系统 DLL（由 OS 提供，无需随包） |
| `KERNEL32.dll` | ❌ 不在 data/player | `MpvPlayerWindow.cs` | 系统 DLL（由 OS 提供，无需随包） |
| `libmpv-2.dll` | ✅ | `MpvImportResolver.cs` | **必须随输出目录提供** |
| `Microsoft.WindowsAppRuntime.dll` | ✅ | `NativeMethods.cs` | **必须随输出目录提供** |
| `mpv` | ❌ 不在 data/player | `MpvNative.cs` | **无扩展名**：加载器按平台补全（Windows 下通常即 libmpv-2.dll 一类）⇒ 需实机确认，勿当成系统 DLL |
| `shell32.dll` | ❌ 不在 data/player | `MpvPlayerWindow.cs` | 系统 DLL（由 OS 提供，无需随包） |
| `USER32.dll` | ❌ 不在 data/player | `MpvPlayerWindow.cs` | 系统 DLL（由 OS 提供，无需随包） |

> **`libmpv-2.dll`（约 112 MB）是本表的头号项**：它不在 `deps.json` 里、不由任何 NuGet target 复制，
> 只能从 `data/player/libmpv-2.dll` 取。缺少它时**托管依赖全齐、播放却起不来或一播就崩**。

### 4.2 deps.json 声明的 native 资产（按 RID 路径）

| 库 | 文件 | 相对路径 | 在 data/player |
|---|---|---|---|
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `Microsoft.DiaSymReader.Native.amd64.dll` | `Microsoft.DiaSymReader.Native.amd64.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `System.IO.Compression.Native.dll` | `System.IO.Compression.Native.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `clretwrc.dll` | `clretwrc.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `clrgc.dll` | `clrgc.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `clrgcexp.dll` | `clrgcexp.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `clrjit.dll` | `clrjit.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `coreclr.dll` | `coreclr.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `createdump.exe` | `createdump.exe` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `hostfxr.dll` | `hostfxr.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `hostpolicy.dll` | `hostpolicy.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `mscordaccore.dll` | `mscordaccore.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `mscordaccore_amd64_amd64_9.0.1426.11910.dll` | `mscordaccore_amd64_amd64_9.0.1426.11910.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `mscordbi.dll` | `mscordbi.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `mscorrc.dll` | `mscorrc.dll` | ✅ |
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/9.0.14` | `msquic.dll` | `msquic.dll` | ✅ |
| `Microsoft.Graphics.Win2D/1.3.2` | `Microsoft.Graphics.Canvas.dll` | `runtimes/win-x64/native/Microsoft.Graphics.Canvas.dll` | ✅ |
| `Microsoft.Web.WebView2/1.0.3179.45` | `WebView2Loader.dll` | `runtimes/win-x64/native/WebView2Loader.dll` | ✅ |
| `Microsoft.WindowsAppSDK.Foundation/1.8.250906002` | `Microsoft.Windows.ApplicationModel.Background.UniversalBGTask.dll` | `runtimes/win-x64/native/Microsoft.Windows.ApplicationModel.Background.UniversalBGTask.dll` | ✅ |
| `Microsoft.WindowsAppSDK.Foundation/1.8.250906002` | `Microsoft.WindowsAppRuntime.Bootstrap.dll` | `runtimes/win-x64/native/Microsoft.WindowsAppRuntime.Bootstrap.dll` | ✅ |
| `Microsoft.WindowsAppSDK.ML/1.8.2091` | `onnxruntime.lib` | `runtimes/win-x64/native/onnxruntime.lib` | ❌ 缺失 |

> `Microsoft.ui.xaml.dll` 等 **WinAppSDK native** 不在此表（也不在 deps.json runtime 段），由
> `WindowsAppSDKSelfContained=true` 的 targets 复制；前提是**显式 `<Platforms>x64</Platforms>`**
> （否则 `GetWindowsAppSDKNativePlatform` 挑不到 `win10-<arch>` 的 Msix）。

## 5. data/player 里其余 .dll（既不属托管面也不属 native 面）

共 **48** 个（占 308 个 .dll 的多数）。
它们主要是 WinAppSDK native 运行库与第三方 native；**不要无脑全抄**（体积大且多数用不上），
按需取：WinAppSDK 那部分交给 SelfContained；确认要用的个别库再单独复制。

<details><summary>展开全部（前 60 项）</summary>

```
CoreMessagingXP.dll
dcompi.dll
DirectML.dll
dwmcorei.dll
DwmSceneI.dll
DWriteCore.dll
libmpv-2.dll
marshal.dll
Microsoft.DirectManipulation.dll
Microsoft.Graphics.Display.dll
Microsoft.Graphics.Imaging.dll
Microsoft.InputStateManager.dll
Microsoft.Internal.FrameworkUdk.dll
Microsoft.UI.Composition.OSSupport.dll
Microsoft.UI.Designer.dll
Microsoft.UI.dll
Microsoft.UI.Input.dll
Microsoft.UI.Windowing.Core.dll
Microsoft.UI.Windowing.dll
Microsoft.UI.Xaml.Controls.dll
Microsoft.ui.xaml.dll
Microsoft.UI.Xaml.Internal.dll
Microsoft.UI.Xaml.Phone.dll
Microsoft.ui.xaml.resources.19h1.dll
Microsoft.ui.xaml.resources.common.dll
Microsoft.Web.WebView2.Core.dll
Microsoft.Windows.AI.ContentSafety.dll
Microsoft.Windows.AI.Imaging.dll
Microsoft.Windows.AI.MachineLearning.dll
Microsoft.Windows.AI.Text.dll
Microsoft.Windows.ApplicationModel.Resources.dll
Microsoft.Windows.Widgets.dll
Microsoft.Windows.Workloads.dll
Microsoft.Windows.Workloads.Resources.dll
Microsoft.Windows.Workloads.Resources_ec.dll
Microsoft.WindowsAppRuntime.dll
Microsoft.WindowsAppRuntime.Insights.Resource.dll
MRM.dll
NPUDetect.dll
onnxruntime.dll
onnxruntime_providers_shared.dll
PushNotificationsLongRunningTask.ProxyStub.dll
SessionHandleIPCProxyStub.dll
WindowsAppRuntime.DeploymentExtensions.OneCore.dll
WindowsAppSdk.AppxDeploymentExtensions.Desktop.dll
WindowsAppSdk.AppxDeploymentExtensions.Desktop-EventLog-Instrumentation.dll
WinUIEdit.dll
wuceffectsi.dll
```
</details>

## 6. 存在性核对（清单声明 vs data/player 实际落地）

| 项 | 值 |
|---|---|
| 托管依赖缺失 | 0 ✅ |
| 本地引用缺失 | 0 ✅ |
| deps.json native 缺失 | 1 |
| data/player 的 .dll 总数 | 308 |

## 7. 落地建议（按推荐度）

1. **首选 M1（链接内核源码）**：`PackageReference` 齐全 ⇒ NuGet 自动写 `<App>.deps.json`，装载器能探测到，**无需 Resolving 补丁**；native（含 `libmpv-2.dll`）仍需按 §4 单独放。
2. **M2（HintPath 引用成品 DLL）**：必须自带「完整托管闭包（§3）+ §2 本地引用 + `Resolving` 补丁」：

   ```csharp
   AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
       var simpleName = new AssemblyName(e.Name).Name;
       var path = Path.Combine(AppContext.BaseDirectory, simpleName + ".dll");
       return File.Exists(path) ? Assembly.LoadFrom(path) : null;
   };
   ```

   > `AssemblyResolve` 只在**装载失败后**触发 ⇒ 它能让程序跑起来，但仍属事后补救；
   > 若 `deps.json` 与复制文件不一致，应优先修 `deps.json`（即改走 M1）。
3. **native 不适用上述机制**：§4.1 判为「必须随输出目录提供」的项（首要是 `libmpv-2.dll`）直接放文件即可，
   它由内核经 P/Invoke 按名加载，不看 deps.json。
4. **不要手工逐个抄 DLL 后靠运气**：这正是 ui 反复试错的路径。

## 8. 机读清单

同目录 `kernel-deps-closure.json` 含逐条记录，字段：`entries`(托管) / `nativeEntries` / `dynNativeEntries` /
`othersNames` / `probeNames`，可直接做 CI 校验。