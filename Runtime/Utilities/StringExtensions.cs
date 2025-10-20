using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.Behavior
{
    internal static class StringExtensions
    {
        internal static string CapitalizeFirstLetter(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var firstChar = char.ToUpper(input[0]);
            var remainingChars = input.Substring(1);
            return firstChar + remainingChars;
        }

        internal static void CopyToClipboard(this string text)
        {
            // Uses IMGUI control.
            TextEditor textEditor = new TextEditor
            {
                text = text
            };

            textEditor.SelectAll();
            textEditor.Copy();
        }
    }
}
