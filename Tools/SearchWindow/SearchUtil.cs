using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIExtras
{
    internal class SearchUtil
    {
        public static string s_MatchFormatPrefix = "<color=#FFFFFF><b>";
        public static string s_MatchFormatSuffix = "</b></color>";
        
        /// <summary>
        /// Checks whether the source string contains the search string, while ignoring case and spaces in both strings. 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="search"></param>
        /// <returns>Integer pairs representing the location and length of matches.</returns>
        public static List<(int, int)> DoesSourceContainSearch(string source, string search)
        {
            List<(int, int)> matches = new List<(int, int)>();
            
            source = source.ToLower();
            search = search.ToLower();
            
            List<string> searchWordsDescendingInLength = search.Split(' ').ToList();
            searchWordsDescendingInLength.Sort((a, b) => b.Length.CompareTo(a.Length));
            
            foreach (string searchWord in searchWordsDescendingInLength)
            {
                if (String.IsNullOrEmpty(searchWord))
                {
                    continue;
                }

                List<int> allOccurrenceOffsets = FindAllOccurrences(searchWord, source);
                
                foreach (var occurrenceOffset in allOccurrenceOffsets)
                {
                    if (!IsIndexInsideAnExistingMatch(occurrenceOffset, matches))
                    {
                        matches.Add((occurrenceOffset, searchWord.Length));
                        break;
                    }
                }
            }
            
            matches.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            return matches;
        }    
        
        private static List<int> FindAllOccurrences(string text, string search)
        {
            List<int> indices = new List<int>();
            int index = search.IndexOf(text);
        
            while (index != -1)
            {
                indices.Add(index);
                index = search.IndexOf(text, index + text.Length);
            }
        
            return indices;
        }

        private static bool IsIndexInsideAnExistingMatch(int index, List<(int, int)> matchOffsets)
        {
            foreach (var matchOffset in matchOffsets)
            {
                if (index >= matchOffset.Item1 && index < matchOffset.Item2)
                {
                    return true;
                }
            }

            return false;
        }

        public static string FormatSearchResult(string text, List<(int, int)> matchOffsets)
        {
            for (int idx = matchOffsets.Count - 1; idx >= 0; idx--)
            {
                (int, int) matchOffset = matchOffsets[idx];
                text = text.Insert(matchOffset.Item1 + matchOffset.Item2, s_MatchFormatSuffix);
                text = text.Insert(matchOffset.Item1, s_MatchFormatPrefix);
            }

            return text;
        }
    }
}