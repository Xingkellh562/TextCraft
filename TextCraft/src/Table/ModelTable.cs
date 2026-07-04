using SixLabors.ImageSharp.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TextCraft.src.Core;
using TextCraft.src.Core.Config;

namespace TextCraft.src.Table
{
    public class Model
    {
        public string Name { get; set; } = "";
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
            Load(System.IO.Path.Combine(AppContext.BaseDirectory + "Config\\Tables\\modelTable.xml"));
        }

        public void Save(string path)
        {
            List<Model> modelList = models.Values.ToList();
            var xs = new XmlSerializer(typeof(List<Model>));
            using var fs = new FileStream(path, FileMode.Create);
            xs.Serialize(fs, modelList);
        }

        public void Load(string path)
        {
            var xs = new XmlSerializer(typeof(List<Model>));
            using var fs = new FileStream(path, FileMode.Open);
            var modelList = (List<Model>?)xs.Deserialize(fs) ?? new List<Model>();
            foreach ( var model in modelList )
                models.Add(model.Name, model);
        }

}}
