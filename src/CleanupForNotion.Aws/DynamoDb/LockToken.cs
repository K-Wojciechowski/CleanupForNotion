using System.Globalization;

namespace CleanupForNotion.Aws.DynamoDb;

public record LockToken(DateTimeOffset Timestamp, Guid Guid) {
  public override string ToString() => $"{Timestamp.ToString("o", CultureInfo.InvariantCulture)}|{Guid}";

  public static LockToken FromString(string s) {
    var splitResult = s.Split("|");
    return new LockToken(
        Timestamp: DateTimeOffset.ParseExact(splitResult[0], "o", CultureInfo.InvariantCulture),
        Guid: Guid.Parse(splitResult[1]));
  }
}
