using System.Text;

namespace CommCodeVerifier.Core;

/// <summary>
/// Изменяемая строка, которая помнит, какие символы ИСХОДНОГО значения были затронуты.
/// Нужна для подсветки красным в исходном коде (и в Excel, и в GUI).
/// </summary>
public sealed class TrackedString
{
    private readonly StringBuilder _sb;
    private readonly List<int> _map;              // _map[i] = индекс в исходной строке или -1 (вставлено)
    private readonly HashSet<int> _touched = new();

    public TrackedString(string source)
    {
        _sb = new StringBuilder(source);
        _map = new List<int>(source.Length);
        for (int i = 0; i < source.Length; i++) _map.Add(i);
    }

    public string Value => _sb.ToString();
    public int Length => _sb.Length;
    public char this[int i] => _sb[i];
    public IReadOnlyCollection<int> TouchedOriginalIndexes => _touched;

    public void MarkTouched(int start, int len)
    {
        for (int i = start; i < start + len && i < _map.Count; i++)
            if (_map[i] >= 0) _touched.Add(_map[i]);
    }

    public void Replace(int start, int len, string replacement)
    {
        if (start < 0 || len < 0 || start + len > _sb.Length)
            throw new ArgumentOutOfRangeException(nameof(start),
                $"Replace(start={start}, len={len}) вне границ строки длиной {_sb.Length}: «{_sb}»");

        MarkTouched(start, len);
        _sb.Remove(start, len);
        _map.RemoveRange(start, len);
        if (replacement.Length > 0)
        {
            _sb.Insert(start, replacement);
            _map.InsertRange(start, Enumerable.Repeat(-1, replacement.Length));
        }
    }

    /// <summary>Заменяет минимально возможный фрагмент (обрезая совпадающие префикс/суффикс).</summary>
    public bool ReplaceSmart(int start, int len, string replacement)
    {
        string current = _sb.ToString(start, len);
        if (string.Equals(current, replacement, StringComparison.Ordinal)) return false;

        int p = 0;
        while (p < current.Length && p < replacement.Length && current[p] == replacement[p]) p++;
        int s = 0;
        while (s < current.Length - p && s < replacement.Length - p &&
               current[current.Length - 1 - s] == replacement[replacement.Length - 1 - s]) s++;

        Replace(start + p, current.Length - p - s, replacement.Substring(p, replacement.Length - p - s));
        return true;
    }
}