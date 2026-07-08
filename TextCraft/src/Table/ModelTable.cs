using SixLabors.ImageSharp.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TextCraft.src.Core;
using TextCraft.src.Core.Config;
using System.Collections.Generic;

namespace TextCraft.src.Table
{


    /// <summary>
    /// 用于交错数组 T[][] 的转换器，使内层数组序列化为单行，外层数组保持整体缩进。
    /// </summary>
    public class JaggedArrayConverter<T> : JsonConverter<T[][]>
    {
        // 用于内层数组序列化的选项（不缩进）
        private static readonly JsonSerializerOptions _innerOptions = new()
        {
            WriteIndented = false
        };

        // 反序列化（标准读取，因为写入格式仍是合法的 JSON）
        public override T[][] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected start of array");

            var result = new List<T[]>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                // 读取内层数组（由于类型是 T[]，不会递归调用本转换器）
                T[]? inner = JsonSerializer.Deserialize<T[]>(ref reader, options);
                result.Add(inner ?? new T[0]);
            }
            return result.ToArray();
        }

        // 序列化
        public override void Write(Utf8JsonWriter writer, T[][] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();  // 外层数组开始

            foreach (var inner in value)
            {
                writer.WriteStartArray();
                foreach (var count in inner)
                {
                    // 将内层数组序列化为不带缩进的 JSON 字符串
                    string countJson = JsonSerializer.Serialize(count, _innerOptions);
                    writer.WriteRawValue(countJson); // 直接写入原始 JSON
                }
                writer.WriteEndArray();
            }

            writer.WriteEndArray();  // 外层数组结束
        }
    }
    public class Model
    {
        public string Name { get; set; } = "";
        [JsonInclude]
        public float[][] faces = new float[][] {} ;

        public Model(float[][] faces,string name)
        {
            this.Name = name;
            this.faces = faces;
        }

        public Model()
        {

        }
    }
    public class QuadFace
    {
        public float[] vertices = new float[] {};

        public QuadFace(float[] vertices)
        {
            this.vertices = (float[])vertices.Clone();
        }

        public QuadFace()
        {
            this.vertices = new float[] { };
        }
    }
    public class ModelTable:BaseSingleton<ModelTable>
    {
        Dictionary<string,Model> models = new Dictionary<string,Model>();
        
        public Model this[string name]
        {
            get 
            {
                if (models.ContainsKey(name))
                    return models[name];
                else
                    return new Model();
            }
        }

        public readonly Model cube = new Model(
            new float[][]
            {
                new float[]{ 
                    // 右面 (x=1) - 三角形1
                    1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                    1.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                    1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                    // 右面 - 三角形2
                    1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                    1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                    1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                },
                new float[]{
                    // 左面 (x=0) - 三角形1
                    0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f,
                    // 左面 - 三角形2
                    0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                },
                new float[]{ 
                    // 上面 (y=1) - 三角形1
                    0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                    1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f,
                    1.0f, 1.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                    // 上面 - 三角形2
                    0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                    1.0f, 1.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                },
                new float[]{
                    // 下面 (y=0) - 三角形1
                    0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                    1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                    1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                    // 下面 - 三角形2
                    0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                    1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                },
                new float[]{
                    // 前面 (z=1) - 三角形1
                    0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                    1.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                    1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f,
                    // 前面 - 三角形2
                    0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                    1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                },
                new float[]{ 
                    // 后面 (z=0) - 三角形1
                    1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                    // 后面 - 三角形2
                    1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                    1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f
                },
            },"cube"
        );

        public readonly Model cube15pxH = new Model(
        new float[][]
        {
            // 右面 (x=1) - 高度和V同步缩放
            new float[]{
                1.0f, 0.0f, 1.0f, 0.0f, 0.9375f, 0.0f,
                1.0f, 0.0f, 0.0f, 1.0f, 0.9375f, 0.0f,
                1.0f, 0.9375f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 1.0f, 0.0f, 0.9375f, 0.0f,
                1.0f, 0.9375f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.9375f, 1.0f, 0.0f, 0.0f, 0.0f
            },
            // 左面 (x=0)
            new float[]{
                0.0f, 0.0f, 0.0f, 0.0f, 0.9375f, 0.0f,
                0.0f, 0.0f, 1.0f, 1.0f, 0.9375f, 0.0f,
                0.0f, 0.9375f, 1.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 0.0f, 0.9375f, 0.0f,
                0.0f, 0.9375f, 1.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.9375f, 0.0f, 0.0f, 0.0f, 0.0f
            },
            // 上面 (y=1) - 仅改高度，UV不变
            new float[]{
                0.0f, 0.9375f, 1.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.9375f, 1.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.9375f, 0.0f, 1.0f, 1.0f, 0.0f,
                0.0f, 0.9375f, 1.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.9375f, 0.0f, 1.0f, 1.0f, 0.0f,
                0.0f, 0.9375f, 0.0f, 0.0f, 1.0f, 0.0f
            },
            // 下面 (y=0) - 高度和UV均不变（y=0，V保持原值）
            new float[]{
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f
            },
            // 前面 (z=1) - 高度和V同步缩放
            new float[]{
                0.0f, 0.0f, 1.0f, 0.0f, 0.9375f, 0.0f,
                1.0f, 0.0f, 1.0f, 1.0f, 0.9375f, 0.0f,
                1.0f, 0.9375f, 1.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f, 0.9375f, 0.0f,
                1.0f, 0.9375f, 1.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.9375f, 1.0f, 0.0f, 0.0f, 0.0f
            },
            // 后面 (z=0) - 高度和V同步缩放
            new float[]{
                1.0f, 0.0f, 0.0f, 0.0f, 0.9375f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 0.9375f, 0.0f,
                0.0f, 0.9375f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 0.0f, 0.0f, 0.9375f, 0.0f,
                0.0f, 0.9375f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 0.9375f, 0.0f, 0.0f, 0.0f, 0.0f
            },
            
        }, "cube15pxH"
    );

        public readonly Model step = new Model(
        new float[][]
         {
             new float[]{ 
                 // 右面 (x=1) - 三角形1
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 0.5f, 0.0f,
                 // 右面 - 三角形2
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 0.5f, 0.0f,
                 1.0f, 0.5f, 1.0f, 0.0f, 0.5f, 0.0f,
             },
             new float[]{
                 // 左面 (x=0) - 三角形1
                 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 // 左面 - 三角形2
                 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 0.0f, 0.5f, 0.0f, 0.0f, 0.5f, 0.0f,
             },
             new float[]{ },
             new float[]{
                 // 下面 (y=0) - 三角形1
                 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 // 下面 - 三角形2
                 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
             },
             new float[]{
                 // 前面 (z=1) - 三角形1
                 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 // 前面 - 三角形2
                 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 0.0f, 0.5f, 1.0f, 0.0f, 0.5f, 0.0f,
             },
             new float[]{ 
                 // 后面 (z=0) - 三角形1
                 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 0.0f, 1.0f, 0.5f, 0.0f,
                 // 后面 - 三角形2
                 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 0.0f, 1.0f, 0.5f, 0.0f,
                 1.0f, 0.5f, 0.0f, 0.0f, 0.5f, 0.0f
             },
             new float[]{ 
                 // 上面 (y=0.5) - 三角形1
                 0.0f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, 0.5f, 1.0f, 1.0f, 0.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 1.0f, 0.0f,
                 // 上面 - 三角形2
                 0.0f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f,
             },
         }, "step"
        );

        public readonly Model stair_posX = new Model(
         new float[][]
         {
             new float[]{ 
                 // 右面 (x=1) - 三角形1
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 0.5f, 0.0f,
                 // 右面 - 三角形2
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 0.5f, 0.0f,
                 1.0f, 0.5f, 1.0f, 0.0f, 0.5f, 0.0f,
                 // 右面 (x=1) - 三角形1
                 1.0f, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f,
                 1.0f, 0.5f, 0.0f, 1.0f, 1.0f, 0.0f,
                 1.0f, 1.0f, 0.0f, 1.0f, 0.5f, 0.0f,
                 // 右面 - 三角形2
                 1.0f, 0.5f, 0.5f, 0.5f, 1.0f, 0.0f,
                 1.0f, 1.0f, 0.0f, 1.0f, 0.5f, 0.0f,
                 1.0f, 1.0f, 0.5f, 0.5f, 0.5f, 0.0f,
             },
             new float[]{
                 // 左面 (x=0) - 三角形1
                 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 // 左面 - 三角形2
                 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 0.0f, 0.5f, 0.0f, 0.0f, 0.5f, 0.0f,
                 // 左面 (x=0) - 三角形1
                 0.0f, 0.5f, 0.0f, 0.0f, 0.5f, 0.0f,
                 0.0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.0f,
                 0.0f, 1.0f, 0.5f, 0.5f, 0.0f, 0.0f,
                 // 左面 - 三角形2
                 0.0f, 0.5f, 0.0f, 0.0f, 0.5f, 0.0f,
                 0.0f, 1.0f, 0.5f, 0.5f, 0.0f, 0.0f,
                 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
             },
             new float[]{ 
                // 上面 (y=1) - 三角形1
                0.0f, 1.0f, 0.5f, 0.0f, 0.5f, 0.0f,
                1.0f, 1.0f, 0.5f, 1.0f, 0.5f, 0.0f,
                1.0f, 1.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                // 上面 - 三角形2
                0.0f, 1.0f, 0.5f, 0.0f, 0.5f, 0.0f,
                1.0f, 1.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
             },
             new float[]{
                 // 下面 (y=0) - 三角形1
                 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 // 下面 - 三角形2
                 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
             },
             new float[]{
                 // 前面 (z=1) - 三角形1
                 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 // 前面 - 三角形2
                 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 1.0f, 0.5f, 1.0f, 1.0f, 0.5f, 0.0f,
                 0.0f, 0.5f, 1.0f, 0.0f, 0.5f, 0.0f,
             },
             new float[]{ 
                 // 后面 (z=0) - 三角形1
                1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                // 后面 - 三角形2
                1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f
             },
             new float[]{ 
                 // 上面 (y=0.5) - 三角形1
                 0.0f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, 0.5f, 1.0f, 1.0f, 0.0f, 0.0f,
                 1.0f, 0.5f, 0.5f, 1.0f, 0.5f, 0.0f,
                 // 上面 - 三角形2
                 0.0f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f,
                 1.0f, 0.5f, 0.5f, 1.0f, 0.5f, 0.0f,
                 0.0f, 0.5f, 0.5f, 0.0f, 0.5f, 0.0f,
                 // 前面 (z=0.5) - 三角形1
                 0.0f, 0.5f, 0.5f, 0.0f, 0.5f, 0.0f,
                 1.0f, 0.5f, 0.5f, 1.0f, 0.5f, 0.0f,
                 1.0f, 1.0f, 0.5f, 1.0f, 0.0f, 0.0f,
                 // 前面 - 三角形2
                 0.0f, 0.5f, 0.5f, 0.0f, 0.5f, 0.0f,
                 1.0f, 1.0f, 0.5f, 1.0f, 0.0f, 0.0f,
                 0.0f, 1.0f, 0.5f, 0.0f, 0.0f, 0.0f,
             },
         }, "stair_posX"
        );

        public ModelTable()
        {
            Load(System.IO.Path.Combine(AppContext.BaseDirectory + "Config\\Tables\\modelTable.json"));
        }

        public void Save(string path)
        {
            List<Model> modelList = models.Values.ToList();
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Converters = {new JaggedArrayConverter<float>()}
            };
            string jsonString = JsonSerializer.Serialize(modelList, options);
            File.WriteAllText(path, jsonString);
        }

        public void Load(string path)
        {
            string jsonString = File.ReadAllText(path);
            List<Model> modelList = JsonSerializer.Deserialize<List<Model>>(jsonString) ?? new();
            //var xs = new XmlSerializer(typeof(List<Model>));
            //using var fs = new FileStream(path, FileMode.Open);
            //var modelList = (List<Model>?)xs.Deserialize(fs) ?? new List<Model>();
            foreach ( var model in modelList )
                models.Add(model.Name, model);
        }

}}
