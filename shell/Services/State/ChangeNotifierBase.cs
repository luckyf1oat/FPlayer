// 服务层最小可观察对象。
// 依据：DESIGN §2 D6（状态对象实现 INotifyPropertyChanged；DI 承载 provider 语义）。
// 说明：服务层为**纯类库**，不引入 CommunityToolkit.Mvvm（那是 App/内核侧依赖），
// 这里用零依赖的等价物承载原 Dart 侧的 ChangeNotifier 语义（notifyListeners → NotifyChanged）。

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIPlayer.Shell.Services.State;

/// <summary>等价 Dart <c>ChangeNotifier</c>：事件 + <see cref="INotifyPropertyChanged"/> 双通道。</summary>
public abstract class ChangeNotifierBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>等价 Dart <c>notifyListeners()</c>（无属性名，监听整体变更）。</summary>
    public event EventHandler Changed;

    protected void NotifyListeners() => NotifyChanged(null);

    protected void NotifyChanged([CallerMemberName] string propertyName = null)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        NotifyChanged(propertyName);
        return true;
    }

    protected void NotifyMany(params string[] propertyNames)
    {
        Changed?.Invoke(this, EventArgs.Empty);
        foreach (var name in propertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
