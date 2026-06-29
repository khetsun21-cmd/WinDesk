namespace WinDesk.Models;

public sealed record Quote(string Name, string Symbol, string Price, DateTimeOffset ReceivedAt);
