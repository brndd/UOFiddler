﻿using System;

namespace UoFiddler.Controls.Helpers
{
    internal static class StringExtensions
    {
        public static bool ContainsCaseInsensitive(this string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
