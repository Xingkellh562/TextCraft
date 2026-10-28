using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TextCraft.src.Tools.ModelImport.Fbx
{
    /// <summary>
    /// FBX 文件解析后的通用节点（二进制与 ASCII 共用）。
    /// Binary FBX 中数组是节点的一个属性；ASCII FBX 中数组写在子块里，
    /// 解析器统一把它们转换成 double[] / long[] 放进 <see cref="Values"/>。
    /// </summary>
    internal sealed class FbxNode
    {
        public string Name = "";

        public readonly List<object> Values = new();

        public readonly List<FbxNode> Children = new();

        /// <summary>ASCII 数组块（例如 Vertices: *24 { ... }）的临时标记。</summary>
        public bool IsArrayContainer;

        public List<object>? ArrayValues;

        public FbxNode? FirstChild(string name)
        {
            foreach (var child in Children)
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }

        public IEnumerable<FbxNode> ChildrenNamed(string name)
        {
            foreach (var child in Children)
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    yield return child;
            }
        }

        public string? GetString(int index)
            => index >= 0 && index < Values.Count ? Values[index] as string : null;

        public long GetLong(int index)
        {
            if (index < 0 || index >= Values.Count)
                return 0;

            return Values[index] switch
            {
                long value => value,
                int value => value,
                short value => value,
                byte value => value,
                double value => (long)value,
                float value => (long)value,
                string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0,
            };
        }

        public double GetDouble(int index)
        {
            if (index < 0 || index >= Values.Count)
                return 0;

            return Values[index] switch
            {
                double value => value,
                float value => value,
                long value => value,
                int value => value,
                short value => value,
                byte value => value,
                string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0,
            };
        }

        /// <summary>取节点上第一个数组值（二进制 FBX 的数组属性）。</summary>
        public object? GetArrayValue()
        {
            foreach (var value in Values)
            {
                if (value is Array)
                    return value;
            }

            return null;
        }

        public double[]? GetDoubleArray(string childName)
            => ToDoubles(FirstChild(childName)?.GetArrayValue());

        public long[]? GetLongArray(string childName)
            => ToLongs(FirstChild(childName)?.GetArrayValue());

        /// <summary>读取 Properties70 下的 P 节点并把其中的数值收集出来（用于 Lcl Translation/Rotation/Scaling）。</summary>
        public bool TryGetPropertyNumbers(string propertyName, out double[] numbers)
        {
            numbers = Array.Empty<double>();
            var properties = FirstChild("Properties70");
            if (properties == null)
                return false;

            foreach (var property in properties.ChildrenNamed("P"))
            {
                if (!string.Equals(property.GetString(0), propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var values = new List<double>();
                for (var i = 1; i < property.Values.Count; i++)
                {
                    if (IsNumber(property.Values[i]))
                        values.Add(ToDouble(property.Values[i]));
                }

                numbers = values.ToArray();
                return numbers.Length > 0;
            }

            return false;
        }

        /// <summary>读取 Properties70 下的 P 节点中的最后一个非空字符串（用于纹理的 RelativeFilename/FileName）。</summary>
        public bool TryGetPropertyString(string propertyName, out string? value)
        {
            value = null;
            var properties = FirstChild("Properties70");
            if (properties == null)
                return false;

            foreach (var property in properties.ChildrenNamed("P"))
            {
                if (!string.Equals(property.GetString(0), propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (var i = 1; i < property.Values.Count; i++)
                {
                    if (property.Values[i] is string text && !string.IsNullOrWhiteSpace(text))
                        value = text;
                }

                return value != null;
            }

            return false;
        }

        /// <summary>把 ASCII 数组块的临时数值转换成数组并写回 Values。</summary>
        public void FinishArray()
        {
            if (!IsArrayContainer || ArrayValues == null)
                return;

            var allIntegers = ArrayValues.All(value => value is long);
            Values.Add(allIntegers
                ? ArrayValues.Cast<long>().ToArray()
                : ArrayValues.Select(ToDouble).ToArray());
            ArrayValues.Clear();
        }

        private static bool IsNumber(object value)
            => value is long or int or short or byte or double or float;

        public static double ToDouble(object value)
        {
            return value switch
            {
                double number => number,
                float number => number,
                long number => number,
                int number => number,
                short number => number,
                byte number => number,
                string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0,
            };
        }

        public static double[]? ToDoubles(object? value)
        {
            switch (value)
            {
                case double[] numbers:
                    return numbers;
                case float[] numbers:
                    return numbers.Select(number => (double)number).ToArray();
                case long[] numbers:
                    return numbers.Select(number => (double)number).ToArray();
                case int[] numbers:
                    return numbers.Select(number => (double)number).ToArray();
                case short[] numbers:
                    return numbers.Select(number => (double)number).ToArray();
                case byte[] numbers:
                    return numbers.Select(number => (double)number).ToArray();
                case System.Collections.IEnumerable enumerable:
                    return enumerable.Cast<object>().Select(ToDouble).ToArray();
                default:
                    return null;
            }
        }

        public static long[]? ToLongs(object? value)
        {
            switch (value)
            {
                case long[] numbers:
                    return numbers;
                case int[] numbers:
                    return numbers.Select(number => (long)number).ToArray();
                case short[] numbers:
                    return numbers.Select(number => (long)number).ToArray();
                case byte[] numbers:
                    return numbers.Select(number => (long)number).ToArray();
                case double[] numbers:
                    return numbers.Select(number => (long)number).ToArray();
                case float[] numbers:
                    return numbers.Select(number => (long)number).ToArray();
                case System.Collections.IEnumerable enumerable:
                    return enumerable.Cast<object>().Select(number => (long)ToDouble(number)).ToArray();
                default:
                    return null;
            }
        }
    }

    internal sealed class FbxConnection
    {
        public string Type = "";

        public long Source;

        public long Destination;

        public string? Property;
    }
}
