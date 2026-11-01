namespace CleanupForNotion.Core;

public static class IDictionaryExtensions {
  public static TV? GetValueOrNull<TK, TV>(this IDictionary<TK, TV> dictionary, TK key) where TV : class =>
      dictionary.TryGetValue(key, out var value) ? value : null;
}
