using Exiled.API.Features.Items;

namespace Slafight_Plugin_EXILED.CustomMaps;

public abstract class FacilityControlRoomFunction
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual int Order => 0;
    public virtual bool UseCooldown => false;
    public virtual float CooldownSeconds => 60f;
    public virtual bool UseExecutionLimit => false;
    public virtual int MaxExecutionCount => 1;

    public virtual void ResetState()
    {
    }

    public abstract FacilityControlRoomFunctionResult Execute(FacilityControlRoomFunctionContext context);

    public virtual string GetCooldownBlockedHint(float remainingSeconds)
    {
        return $"<size=24>{DisplayName} はクールダウン中です。\n残り {remainingSeconds:F0} 秒</size>";
    }

    public virtual string GetExecutionLimitBlockedHint(int executedCount)
    {
        return $"<size=24>{DisplayName} は使用回数上限に達しています。\n使用回数 {executedCount}/{MaxExecutionCount}</size>";
    }

    protected static FacilityControlRoomFunctionResult Success(string hint)
    {
        return new FacilityControlRoomFunctionResult(hint, true);
    }

    protected static FacilityControlRoomFunctionResult Failure(string hint)
    {
        return new FacilityControlRoomFunctionResult(hint, false);
    }

    protected static FacilityControlRoomFunctionResult SilentSuccess()
    {
        return new FacilityControlRoomFunctionResult(string.Empty, true);
    }

    protected static FacilityControlRoomFunctionResult SilentFailure()
    {
        return new FacilityControlRoomFunctionResult(string.Empty, false);
    }
}

public readonly struct FacilityControlRoomFunctionContext
{
    public FacilityControlRoomFunctionContext(
        Exiled.API.Features.Player player,
        Keycard stagedKeycard,
        int executedCount)
    {
        Player = player;
        StagedKeycard = stagedKeycard;
        ExecutedCount = executedCount;
    }

    public Exiled.API.Features.Player Player { get; }
    public Keycard StagedKeycard { get; }
    public int ExecutedCount { get; }
}

public readonly struct FacilityControlRoomFunctionResult
{
    public FacilityControlRoomFunctionResult(string hint, bool countAsExecution)
    {
        Hint = hint;
        CountAsExecution = countAsExecution;
    }

    public string Hint { get; }
    public bool CountAsExecution { get; }
}
