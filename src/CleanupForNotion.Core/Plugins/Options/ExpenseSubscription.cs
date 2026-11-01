namespace CleanupForNotion.Core.Plugins.Options;

public class ExpenseSubscription {
  public required string[] Names { get; init; }
  public required int ExpectedTotal { get; init; }
}
