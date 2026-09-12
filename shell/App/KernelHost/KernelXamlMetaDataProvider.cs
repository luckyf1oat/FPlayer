using System;
using Microsoft.UI.Xaml.Markup;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// 把内核的 XamlTypeInfo 串进本外壳的元数据提供器链。
///
/// <para>**问题**（2026-09-11 运行时实测，非推断）：M1 把内核反编译源码编进**同一个**程序集后，
/// 外壳的 XamlCompiler 只为「**其它程序集**里带 XamlMetaDataProvider 的引用」生成串接项
/// （`obj\…\XamlTypeInfo.g.cs` L3440-3459：`Microsoft.UI.Xaml.XamlTypeControls` /
/// `CommunityToolkit.WinUI.Controls.Sizers` / `Richasy.MpvKernel.WinUI` /
/// `Richasy.WinUIKernel.Share` / `WinUIEx` 五个），**不含**内核自己的
/// `WinUISample.AIPlayer_MpvHost_XamlTypeInfo`（它与外壳同处一个程序集）。
/// 后果：内核 XBF 解析时按类型名查不到类型 ⇒
/// `TARGET-FAIL … XamlParseException`，`HRESULT 0x802B000A`。</para>
///
/// <para>实测判据（同一次运行内同时采到，互为对照）：
/// <code>
/// PROVIDER app IXamlMetadataProvider=ok  GetXamlType(WinUISample.Controls.PlayerOverlayBase)=null
/// PROVIDER kernel-type=FOUND WinUISample.AIPlayer_MpvHost_XamlTypeInfo.XamlTypeInfoProvider
/// PROVIDER kernel GetXamlTypeByName(WinUISample.Controls.PlayerOverlayBase)=XamlUserType
/// </code>
/// 即「内核提供器认识该类型，但运行中的 Application 不认」。</para>
///
/// <para>**本类**是补链件：内核那个 `XamlTypeInfoProvider` 只暴露
/// `GetXamlTypeByName(string)` / `GetXamlTypeByType(Type)`，本身**不实现**
/// `IXamlMetadataProvider`（接口实现由生成代码里的 `XamlMetaDataProvider` 包装类承担，
/// 而内核那份没有对应的包装类可用），故在此补一个包装。
/// 注入动作由构建期脚本 `tools\chain-kernel-xamltinfo.ps1` 完成（见 csproj 的
/// `ChainKernelXamlMetadataProvider` 目标）。</para>
/// </summary>
public sealed class KernelXamlMetaDataProvider : IXamlMetadataProvider
{
    // 同程序集 ⇒ 即使内核那个类是 internal 也可直接构造。
    private readonly WinUISample.AIPlayer_MpvHost_XamlTypeInfo.XamlTypeInfoProvider _inner =
        new WinUISample.AIPlayer_MpvHost_XamlTypeInfo.XamlTypeInfoProvider();

    public IXamlType GetXamlType(Type type)
    {
        return type == null ? null : _inner.GetXamlTypeByName(type.FullName);
    }

    public IXamlType GetXamlType(string fullName)
    {
        return string.IsNullOrEmpty(fullName) ? null : _inner.GetXamlTypeByName(fullName);
    }

    /// <summary>
    /// 内核的 XmlnsDefinition 集合在本工程内由外壳自己的 XamlTypeInfo 承载，
    /// 这里返回空数组即可 —— XBF 解析路径用的是 <c>GetXamlType(string)</c>。
    /// </summary>
    public XmlnsDefinition[] GetXmlnsDefinitions()
    {
        return Array.Empty<XmlnsDefinition>();
    }
}
