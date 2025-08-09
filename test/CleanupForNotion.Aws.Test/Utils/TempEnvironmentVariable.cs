namespace CleanupForNotion.Aws.Test.Utils;

public class TempEnvironmentVariable : IDisposable {
  private readonly string _name;
  private readonly string? _oldValue;
  public TempEnvironmentVariable(string name, string? value) {
    _name = name;
    _oldValue = Environment.GetEnvironmentVariable(name);
    Environment.SetEnvironmentVariable(name, value);
  }

  public void Dispose() {
    Environment.SetEnvironmentVariable(_name, _oldValue);
    GC.SuppressFinalize(this);
  }
}
