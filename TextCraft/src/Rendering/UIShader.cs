using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Rendering
{
    internal class UIShader : BaseShader
    {
        //顶点着色器
        protected override string VertexShaderSource => @"
            #version 450 core
            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec3 aTexCoord;

            out vec2 TexCoord;
            out float ao;

            uniform mat4 projection;

            void main()
            {
                gl_Position = projection * vec4(aPos , 1.0);
                TexCoord = aTexCoord.xy;
                ao = aTexCoord.z;
            }";
        //片段着色器
        protected override string FragmentShaderSource => @"
            #version 450 core
            in vec2 TexCoord;
            in float ao;

            out vec4 FragColor;

            uniform sampler2D ourTexture;

            void main()
            {
                vec4 t = texture(ourTexture,TexCoord);
                vec3 lib = t.rgb * ao;
                FragColor = vec4(lib,t.a);
            }";

        public override void Draw()
        {
            
        }
    }
}
