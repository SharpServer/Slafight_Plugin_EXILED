namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// ラウンド中に排他的に実行されるシーケンスの定義です。操作はサーバーのメインスレッドで行います。
/// </summary>
public abstract class SequenceBase
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual byte Priority { get; set; } = 10;

    /// <summary>実行中に、より高い優先度のシーケンスへの交代を許可します。</summary>
    public virtual bool AllowOverride { get; set; }

    /// <summary>ラウンド状態、割り込み優先度、固有の開始条件をすべて確認します。</summary>
    public bool CanTrigger() => SequenceManager.CanStart(this);

    public bool TriggerSequence() => SequenceManager.TryStart(this);

    /// <summary>人数など、シーケンス固有の開始条件だけを副作用なしで判定します。</summary>
    protected virtual bool MeetsTriggerConditions() => true;

    /// <summary>
    /// 開始時に呼ばれます。遅延処理にはこの実行の scope を渡し、完了時に scope.Complete() を呼びます。
    /// </summary>
    protected virtual void OnTrigger(SequenceRun scope) { }

    /// <summary>実行に紐づく処理と後始末が片付いたあとに呼ばれます。</summary>
    protected virtual void OnEnd(SequenceEndReason reason) { }

    internal bool CheckTriggerConditions() => MeetsTriggerConditions();
    internal void Start(SequenceRun scope) => OnTrigger(scope);
    internal void End(SequenceEndReason reason) => OnEnd(reason);
}
