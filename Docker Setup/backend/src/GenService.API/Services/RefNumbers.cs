namespace GenService.API.Services;

/// <summary>
/// Next reference number in a series (REQ-2026-0004, V/26/057, DDT/26/165…).
///
/// References used to be "count of records + 1". That breaks as soon as any
/// record is deleted: the count drops, the next number repeats one already in
/// use, and the save fails on the unique index — which is how staff came to see
/// "Failed to submit request" after old test requests were deleted. Numbering
/// from the highest existing number can never collide, and never re-issues a
/// number that was used before.
/// </summary>
public static class RefNumbers
{
    /// <param name="prefix">Everything before the running number, e.g. "REQ-2026-".</param>
    /// <param name="existing">References already issued with that prefix.</param>
    /// <param name="pad">Minimum digits in the running number.</param>
    public static string Next(string prefix, IEnumerable<string?> existing, int pad)
    {
        var max = 0;
        foreach (var r in existing)
        {
            if (r is null || !r.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            // Leading digits only, so a suffixed reference such as "V/26/003 B" still counts as 3.
            var digits = new string(r[prefix.Length..].TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var n) && n > max) max = n;
        }
        return prefix + (max + 1).ToString("D" + pad);
    }
}
