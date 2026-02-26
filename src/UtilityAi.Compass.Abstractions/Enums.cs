namespace UtilityAi.Compass.Abstractions;

public enum GoalTag { Answer, Clarify, Summarize, Execute, Approve, Stop }
public enum Lane { Interpret, Plan, Execute, Communicate, Safety, Housekeeping }
public enum SideEffectLevel { ReadOnly, Write, Destructive }
public enum OutcomeTag { Success, Failure, Skipped, Escalated }
public enum CliVerb { Read, Write, Update }
