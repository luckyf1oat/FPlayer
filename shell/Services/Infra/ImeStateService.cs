// S5 体验设施（t33）：IME（中文输入法）在搜索框里的行为 —— 服务侧提供**状态**，UI 按状态决定回车语义。
// 关键语义（原版同款）：**组字中按 Enter ⇒ 只提交候选字，不触发搜索**；组字结束后的 Enter 才搜索。
using System;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>
/// 输入法组字状态机（供搜索框使用）。**不依赖任何 UI 类型** —— UI 把 IME 事件喂进来，读状态决定行为。
/// </summary>
public sealed class ImeStateService
{
    /// <summary>是否正在组字（候选未定）。</summary>
    public bool IsComposing { get; private set; }

    /// <summary>当前组字串（未组字时为空串）。</summary>
    public string CompositionText { get; private set; } = string.Empty;

    /// <summary>最近一次提交的文本。</summary>
    public string LastCommitted { get; private set; } = string.Empty;

    /// <summary>状态变化通知（UI 可据此刷新提示）。</summary>
    public event Action Changed;

    /// <summary>**回车语义**：组字中 ⇒ false（只提交候选、不搜）；否则 ⇒ true。</summary>
    public bool ShouldTriggerSearchOnEnter => !IsComposing;

    /// <summary>**清空语义**：组字中 ⇒ 只清组字串（不动已输入内容）；否则 ⇒ 清搜索框。</summary>
    public bool ShouldClearInputOnEscape => !IsComposing;

    public void BeginComposition(string text = null)
    {
        IsComposing = true;
        CompositionText = text ?? string.Empty;
        Changed?.Invoke();
    }

    public void UpdateComposition(string text)
    {
        IsComposing = true;
        CompositionText = text ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>结束组字并提交。<paramref name="committed"/> 为空时沿用当前组字串。</summary>
    public string EndComposition(string committed = null)
    {
        var value = committed ?? CompositionText ?? string.Empty;
        IsComposing = false;
        CompositionText = string.Empty;
        LastCommitted = value;
        Changed?.Invoke();
        return value;
    }

    /// <summary>失焦/切屏时强制复位（避免状态卡在"组字中"导致回车永远只提交候选）。</summary>
    public void Reset()
    {
        if (!IsComposing && CompositionText.Length == 0) return;
        IsComposing = false;
        CompositionText = string.Empty;
        Changed?.Invoke();
    }
}
