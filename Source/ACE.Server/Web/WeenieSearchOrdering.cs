using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Web
{
    /// <summary>Orders weenie search: literal prefix, then word prefix, then earliest substring match.</summary>
    public static class WeenieSearchOrdering
    {
        public const int RankNameStartsWith = 0;
        public const int RankClassNameStartsWith = 1;
        public const int RankWordStartsWithName = 2;
        public const int RankWordStartsWithClassName = 3;
        public const int RankNameContains = 4;
        public const int RankClassNameContains = 5;
        public const int RankOther = 6;

        /// <summary>Lower rank is better; position is index of match in the full field (earlier = better).</summary>
        public static (int rank, int position) GetMatchScore(string query, string name, string className)
        {
            if (string.IsNullOrWhiteSpace(query))
                return (RankOther, int.MaxValue);

            var q = query.Trim();
            var cmp = StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrEmpty(name) && name.StartsWith(q, cmp))
                return (RankNameStartsWith, 0);
            if (!string.IsNullOrEmpty(className) && className.StartsWith(q, cmp))
                return (RankClassNameStartsWith, 0);

            var wordPos = GetEarliestWordStartingWith(name, q);
            if (wordPos >= 0)
                return (RankWordStartsWithName, wordPos);

            wordPos = GetEarliestWordStartingWith(className, q);
            if (wordPos >= 0)
                return (RankWordStartsWithClassName, wordPos);

            var idx = IndexOfIgnoreCase(name, q);
            if (idx >= 0)
                return (RankNameContains, idx);

            idx = IndexOfIgnoreCase(className, q);
            if (idx >= 0)
                return (RankClassNameContains, idx);

            return (RankOther, int.MaxValue);
        }

        public static int GetMatchRank(string query, string name, string className) =>
            GetMatchScore(query, name, className).rank;

        public static List<T> SortByRelevance<T>(
            IEnumerable<T> source,
            string query,
            Func<T, string> getName,
            Func<T, string> getClassName,
            Func<T, uint> getWcid,
            int limit)
        {
            uint? exactWcid = uint.TryParse(query?.Trim(), out var w) ? w : null;

            return source
                .Select(x =>
                {
                    var score = GetMatchScore(query, getName(x), getClassName(x));
                    var exact = exactWcid.HasValue && getWcid(x) == exactWcid.Value;
                    return new { Item = x, Exact = exact, score.rank, score.position };
                })
                .OrderBy(x => x.Exact ? -1 : x.rank)
                .ThenBy(x => x.position)
                .ThenBy(x => getName(x.Item), StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => getWcid(x.Item))
                .Take(limit)
                .Select(x => x.Item)
                .ToList();
        }

        public static List<string> SortStringsByRelevance(IEnumerable<string> source, string query, int limit)
        {
            if (string.IsNullOrWhiteSpace(query))
                return source.Take(limit).ToList();

            var q = query.Trim();
            var cmp = StringComparison.OrdinalIgnoreCase;

            return source
                .OrderBy(s =>
                {
                    if (string.IsNullOrEmpty(s)) return (1, int.MaxValue);
                    if (s.StartsWith(q, cmp)) return (0, 0);
                    var wordPos = GetEarliestWordStartingWith(s, q);
                    if (wordPos >= 0) return (1, wordPos);
                    var idx = IndexOfIgnoreCase(s, q);
                    return idx >= 0 ? (2, idx) : (3, int.MaxValue);
                })
                .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }

        /// <summary>Character index in <paramref name="text"/> where a token starts with <paramref name="q"/>.</summary>
        private static int GetEarliestWordStartingWith(string text, string q)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(q))
                return -1;

            var cmp = StringComparison.OrdinalIgnoreCase;
            var best = -1;

            for (var i = 0; i < text.Length; i++)
            {
                if (i > 0 && IsWordChar(text[i - 1]))
                    continue;

                var end = i;
                while (end < text.Length && IsWordChar(text[end]))
                    end++;

                if (end > i)
                {
                    var len = end - i;
                    if (len >= q.Length && string.Compare(text, i, q, 0, q.Length, cmp) == 0)
                    {
                        if (best < 0 || i < best)
                            best = i;
                    }
                }

                i = end;
            }

            return best;
        }

        private static bool IsWordChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_' || c == '\'' || c == '-';

        private static int IndexOfIgnoreCase(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
                return -1;
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        }

        public static string EscapeLikeLiteral(string q) =>
            q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        public static string ContainsLikePattern(string q) => $"%{EscapeLikeLiteral(q)}%";
    }
}
