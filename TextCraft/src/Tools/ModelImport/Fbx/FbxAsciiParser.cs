using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TextCraft.src.Tools.ModelImport.Fbx
{
    /// <summary>
    /// ASCII FBX 解析器（尽力而为）。按行处理常见导出器格式：
    /// <c>Node: values { ... }</c>、<c>Node: *count { a: numbers }</c>、<c>C: "OO",id,id</c>。
    /// 二进制 FBX 请使用 <see cref="FbxBinaryParser"/>。
    /// </summary>
    internal static class FbxAsciiParser
    {
        public static FbxNode Parse(string text)
        {
            var root = new FbxNode { Name = "<root>" };
            var stack = new Stack<FbxNode>();
            stack.Push(root);

            using var reader = new StringReader(text);
            string? rawLine;
            while ((rawLine = reader.ReadLine()) != null)
            {
                var line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                    continue;

                var closeCount = CountTrailingClosers(ref line);
                line = line.Trim();

                if (line.Length == 0)
                {
                    PopMany(stack, closeCount);
                    continue;
                }

                var current = stack.Peek();

                // 数组块内部：只收集数值，直到对应的 }
                if (current.IsArrayContainer && current.ArrayValues != null)
                {
                    var valueText = line;
                    var colon = IndexOfColonOutsideQuotes(line);
                    if (colon >= 0)
                        valueText = line[(colon + 1)..];

                    AppendNumbers(current, valueText);
                    PopMany(stack, closeCount);
                    continue;
                }

                // 块开在下一行：归属于最近添加的子节点
                if (line == "{")
                {
                    if (current.Children.Count > 0)
                        stack.Push(current.Children[^1]);
                    continue;
                }

                if (line.EndsWith("{"))
                {
                    var node = ParseStatement(line[..^1].Trim());
                    if (node != null)
                    {
                        current.Children.Add(node);
                        stack.Push(node);
                    }

                    // 兼容 Properties70: {} 这类写在同一行的空块
                    PopMany(stack, closeCount);
                    continue;
                }

                var leaf = ParseStatement(line);
                if (leaf != null)
                    current.Children.Add(leaf);

                PopMany(stack, closeCount);
            }

            while (stack.Count > 1)
                Pop(stack);

            return root;
        }

        private static FbxNode? ParseStatement(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return null;

            var colon = IndexOfColonOutsideQuotes(text);
            if (colon < 0)
                return new FbxNode { Name = text };

            var node = new FbxNode { Name = text[..colon].Trim() };
            var valueText = text[(colon + 1)..].Trim();

            if (valueText.StartsWith("*", StringComparison.Ordinal))
            {
                node.IsArrayContainer = true;
                node.ArrayValues = new List<object>();
                return node;
            }

            node.Values.AddRange(ParseValues(valueText));
            return node;
        }

        private static List<object> ParseValues(string text)
        {
            var result = new List<object>();
            var index = 0;

            while (index < text.Length)
            {
                while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
                    index++;

                if (index >= text.Length)
                    break;

                if (text[index] == '"')
                {
                    index++;
                    var builder = new StringBuilder();
                    while (index < text.Length)
                    {
                        var ch = text[index];
                        if (ch == '\\' && index + 1 < text.Length && (text[index + 1] == '"' || text[index + 1] == '\\'))
                        {
                            builder.Append(text[index + 1]);
                            index += 2;
                            continue;
                        }

                        if (ch == '"')
                        {
                            index++;
                            break;
                        }

                        builder.Append(ch);
                        index++;
                    }

                    result.Add(builder.ToString());
                    continue;
                }

                var start = index;
                while (index < text.Length && text[index] != ',')
                    index++;

                var token = text[start..index].Trim();
                if (token.Length == 0)
                    continue;

                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    result.Add(integer);
                else if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    result.Add(number);
                else
                    result.Add(token);
            }

            return result;
        }

        private static void AppendNumbers(FbxNode node, string text)
        {
            foreach (var value in ParseValues(text))
            {
                if (value is long || value is double)
                    node.ArrayValues!.Add(value);
            }
        }

        private static int IndexOfColonOutsideQuotes(string text)
        {
            var inQuotes = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\\' && inQuotes && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ':' && !inQuotes)
                    return i;
            }

            return -1;
        }

        private static string StripComment(string line)
        {
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '\\' && inQuotes && i + 1 < line.Length)
                {
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ';' && !inQuotes)
                    return line[..i];
            }

            return line;
        }

        private static int CountTrailingClosers(ref string line)
        {
            var count = 0;
            var end = line.Length;
            while (end > 0)
            {
                var ch = line[end - 1];
                if (ch == '}')
                {
                    count++;
                    end--;
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    end--;
                    continue;
                }

                break;
            }

            line = line[..end];
            return count;
        }

        private static void PopMany(Stack<FbxNode> stack, int count)
        {
            for (var i = 0; i < count; i++)
                Pop(stack);
        }

        private static void Pop(Stack<FbxNode> stack)
        {
            if (stack.Count <= 1)
                return;

            var node = stack.Pop();
            node.FinishArray();
        }
    }
}
