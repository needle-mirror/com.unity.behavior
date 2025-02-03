using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIExtras
{
    internal class SearchUtil
    {
        internal struct Match
        {
            public int Offset;
            public int Length;
            public int Index;

            public Match(int offset, int length, int index)
            {
                Offset = offset;
                Length = length;
                Index = index;
            }
        }
        
        public static string s_MatchFormatPrefix = "<color=#FFFFFF><b>";
        public static string s_MatchFormatSuffix = "</b></color>";

        private static List<(int, int)> FindAllOccurrences(string query, string candidate)
        {
            List<(int, int)> indices = new List<(int, int)>();
            (int, int) indexLength = ContainsIgnoringSpaces(query, candidate, 0);

            while (indexLength.Item1 != -1)
            {
                indices.Add(indexLength);
                indexLength = ContainsIgnoringSpaces(query, candidate, indexLength.Item1 + query.Length);
            }

            return indices;
        }

        /// <summary>
        /// Performs a word-based search for the words in query in candidate.
        /// </summary>
        /// <param name="query">Query to look for.</param>
        /// <param name="candidate">Target string to look in.</param>
        /// <returns>List of index/length pairs found, if any.</returns>
        internal static List<Match> DoesSourceContainSearchWholeWords(string query, string candidate)
        {
            List<Match> matches = new List<Match>();

            query = query.ToLower();
            candidate = candidate.ToLower();

            List<string> queryWordsDescendingLength = query.Split(' ').ToList();
            queryWordsDescendingLength.Sort((a, b) => b.Length.CompareTo(a.Length));

            for (int queryWordIndex = 0; queryWordIndex < queryWordsDescendingLength.Count; ++queryWordIndex)
            {
                string queryWord = queryWordsDescendingLength[queryWordIndex];
                
                if (String.IsNullOrEmpty(queryWord))
                {
                    continue;
                }

                List<(int, int)> allOccurrenceOffsetLengths = FindAllOccurrences(queryWord, candidate);

                foreach (var occurrenceOffset in allOccurrenceOffsetLengths)
                {
                    if (!IsIndexInsideAnExistingMatch(occurrenceOffset.Item1, matches))
                    {
                        Match result = new Match();
                        result.Offset = occurrenceOffset.Item1;
                        result.Length = occurrenceOffset.Item2;
                        result.Index = queryWordIndex;
                        
                        matches.Add(result);
                        break;
                    }
                }
            }

            matches.Sort((a, b) =>
            {
                if (a.Offset != b.Offset)
                {
                    return a.Offset.CompareTo(b.Offset);
                }
                
                return b.Index.CompareTo(a.Index);
            });
            
            return matches;
        }

        /// <summary>
        /// Checks whether an index is in any index/length pairs.
        /// </summary>
        /// <param name="index">Index to check.</param>
        /// <param name="matchIndexLengths">index/length pairs to check.</param>
        /// <returns></returns>
        private static bool IsIndexInsideAnExistingMatch(int index, List<Match> matchIndexLengths)
        {
            foreach (var entry in matchIndexLengths)
            {
                if (index >= entry.Offset && index < (entry.Offset + entry.Length))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if 'query' is contained in 'testString' while ignoring
        /// spaces in 'testString'. This is a custom implementation that does
        /// not remove spaces; it skips them during matching.
        /// </summary>
        /// <param name="testString">The string in which to search.</param>
        /// <param name="query">The substring/query to find.</param>
        /// <param name="offset">Index in the testString to start looking.</param>
        /// <returns>
        /// The index or -1 if not found.
        /// </returns>
        public static (int, int) ContainsIgnoringSpaces(string query, string testString, int offset = 0)
        {
            if (string.IsNullOrEmpty(testString) || string.IsNullOrEmpty(query))
                return (-1, -1);

            // Try to match 'query' starting at each position of 'testString'
            for (int startIndex = offset; startIndex < testString.Length; startIndex++)
            {
                if (char.IsWhiteSpace(testString[startIndex]))
                {
                    continue;
                }
                
                int testIndex = startIndex;
                int queryIndex = 0;

                // Keep matching characters until we exhaust testString or query
                while (testIndex < testString.Length && queryIndex < query.Length)
                {
                    // Skip spaces in the test string
                    if (char.IsWhiteSpace(testString[testIndex]))
                    {
                        testIndex++;
                        continue;
                    }

                    // Compare current characters (case-insensitive)
                    if (char.ToUpperInvariant(testString[testIndex]) ==
                        char.ToUpperInvariant(query[queryIndex]))
                    {
                        queryIndex++;
                    }
                    else
                    {
                        // Not a match here, break and move to the next startIndex
                        break;
                    }

                    testIndex++;
                }

                // If we've matched the entire query, return true
                if (queryIndex == query.Length)
                    return (startIndex, testIndex - startIndex);
            }

            // If we tried every start index and never matched the full query, return false
            return (-1, -1);
        }

        /// <summary>
        /// Applies the rich text formatting to text based on the match data.
        /// </summary>
        /// <param name="text">Text to format.</param>
        /// <param name="matchIndexLengths">Match data.</param>
        /// <returns>Formatted string.</returns>
        internal static string FormatSearchResult(string text, List<Match> matchIndexLengths)
        {
            for (int idx = matchIndexLengths.Count - 1; idx >= 0; idx--)
            {
                Match matchOffset = matchIndexLengths[idx];
                text = text.Insert(matchOffset.Offset + matchOffset.Length, s_MatchFormatSuffix);
                text = text.Insert(matchOffset.Offset, s_MatchFormatPrefix);
            }

            return text;
        }

        /// <summary>
        /// Compares two int pairs where the first integer is the index and the second is the length. The first integer
        /// is compared ascending and the second descending. For example:
        ///
        /// (1, 4), (0, 5), (0, 3)
        ///
        /// would be sorted to:
        ///
        /// (0, 5), (0, 3), (1, 4)
        /// </summary>
        /// <param name="a">First pair.</param>
        /// <param name="b">Second pair.</param>
        /// <returns>Comparison result.</returns>
        internal static int CompareIndexLengthPair((int, int) a, (int, int) b)
        {
            int indexCompare = a.Item1.CompareTo(b.Item1);

            if (indexCompare != 0)
                return indexCompare;

            return b.Item2.CompareTo(a.Item2);
        }
    }
}