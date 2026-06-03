using System;

namespace Cysharp.Text
{
    public partial struct Utf16ValueStringBuilder
    {
        public static bool IsEscapeChar(char c)
        {
            return c == '[' || c == ']' || c == '\\';
        }
        
        /// <summary>
        /// 删除转义符
        /// </summary>
        /// <param name="value"></param>
        public void AppendCancelEscape(ReadOnlySpan<char> value)
        {
            var length = value.Length;
            var startIndex = 0;
            for (int i = 0, len = length - 1; i < len; i++)
            {
                if (value[i] == '\\' && IsEscapeChar(value[i+1]))
                {
                    Append(value.Slice(startIndex, i - startIndex));
                    Append(value[++i]);
                    startIndex = i+1;
                }
            }

            if (length > 1 && startIndex < length)
            {
                Append(value.Slice(startIndex, length - startIndex));
            }
        }

        /// <summary>
        /// 增加转义符
        /// </summary>
        /// <param name="value"></param>
        public void AppendWithEscape(ReadOnlySpan<char> value)
        {
            var length = value.Length;
            for (int i = 0; i < length; i++)
            {
                if (IsEscapeChar(value[i]))
                {
                    Append('\\');
                }
                Append(value[i]);
            }
        }
    }
}
