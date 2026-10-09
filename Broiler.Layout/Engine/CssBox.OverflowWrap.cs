using System;
using System.Collections.Generic;
using System.Globalization;

namespace Broiler.Layout.Engine;

internal partial class CssBox
{
    private CssRect[]? _unwrappedWords;

    internal void RestoreUnwrappedWords()
    {
        if (_unwrappedWords is null) return;
        Words.Clear();
        Words.AddRange(_unwrappedWords);
        _unwrappedWords = null;
        _wordsSizeMeasured = false;
    }

    // Emergency breaks are introduced only when a word cannot fit on a whole line.
    // Keep original words so resizing to a wider block does not retain old breaks.
    internal void WrapOversizedWords(ILayoutEnvironment environment, double available)
    {
        if (OverflowWrap is not ("break-word" or "anywhere") || WhiteSpace is "nowrap" or "pre"
            || available <= 0 || !double.IsFinite(available)) return;
        for (var i = 0; i < Words.Count; i++)
        {
            var word = Words[i];
            if (word.IsImage || word.IsLineBreak || word.Width <= available || string.IsNullOrEmpty(word.Text)) continue;
            var offsets = StringInfo.ParseCombiningCharacters(word.Text);
            if (offsets.Length < 2) continue;
            _unwrappedWords ??= Words.ToArray();
            var pieces = new List<CssRect>();
            for (var start = 0; start < offsets.Length;)
            {
                var low = start + 1; var high = offsets.Length;
                while (low < high)
                {
                    var mid = (low + high + 1) / 2;
                    var end = mid == offsets.Length ? word.Text.Length : offsets[mid];
                    if (environment.MeasureText(ActualFont, word.Text[offsets[start]..end]).Width <= available) low = mid;
                    else high = mid - 1;
                }
                var limit = low == offsets.Length ? word.Text.Length : offsets[low];
                var text = word.Text[offsets[start]..limit];
                var measured = environment.MeasureText(ActualFont, text);
                pieces.Add(new CssRectWord(this, text, start == 0 && word.HasSpaceBefore,
                    low == offsets.Length && word.HasSpaceAfter) { Width = measured.Width, Height = word.Height });
                start = low;
            }
            Words.RemoveAt(i);
            Words.InsertRange(i, pieces);
            i += pieces.Count - 1;
        }
    }
}
