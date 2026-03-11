namespace PampaSkylines.Simulation
{
using System.Collections.Generic;
using PampaSkylines.Commands;

public enum CommandExecutionStatus
{
    Applied = 0,
    Rejected = 1
}

public enum CommandRejectionReason
{
    None = 0,
    InvalidPayload = 1,
    UnsupportedRoadType = 2,
    UnsupportedZoneType = 3,
    UnsupportedServiceType = 4,
    InsufficientFunds = 5,
    DuplicateRoad = 6,
    TargetNotFound = 7,
    InvalidTaxRate = 8,
    InvalidTimeScale = 9,
    ProgressionLocked = 10,
    NoActiveEvent = 11,
    InvalidEventChoice = 12,
    GameOverLocked = 13
}

public sealed class CommandExecutionResult
{
    public string CommandId { get; set; } = string.Empty;

    public GameCommandType Type { get; set; }

    public CommandExecutionStatus Status { get; set; }

    public CommandRejectionReason RejectionReason { get; set; }

    public string Message { get; set; } = string.Empty;

    public decimal CashDelta { get; set; }

    public long AppliedAtTick { get; set; }
}

public sealed class SimulationFrameReport
{
    public long TickBefore { get; set; }

    public long TickAfter { get; set; }

    public float DeltaTime { get; set; }

    public string SimulationConfigVersion { get; set; } = string.Empty;

    public string StateHashBefore { get; set; } = string.Empty;

    public string StateHashAfter { get; set; } = string.Empty;

    public int RequestedCommandCount { get; set; }

    public int AppliedCommandCount { get; set; }

    public int RejectedCommandCount { get; set; }

    public List<CommandExecutionResult> CommandResults { get; set; } = new();

    public List<SimulationEvent> SimulationEvents { get; set; } = new();
}

public sealed class SimulationEvent
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
}
