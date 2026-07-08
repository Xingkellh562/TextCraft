using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TextCraft.src.Rendering.OIT
{
    internal class CompositeShader : BaseShader
    {
        protected override string VertexShaderSource => @"
            #version 450 core
            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec3 aTexCoord; 

            out vec2 TexCoord;

            void main()
            {
                gl_Position = vec4(aPos , 1.0);
                TexCoord = aTexCoord.xy;
            }
        ";
         
        protected override string FragmentShaderSource => @"
            #version 450 core
            in vec2 TexCoord;

            out vec4 FragColor;
            
            uniform sampler2D accumTex;
            uniform sampler2D revealTex;
        
            void main()
            {
                vec4 accum = texture(accumTex,TexCoord);
                float reveal = texture(revealTex,TexCoord).r;

                vec3 avgColor = accum.rgb / accum.a;

                float alpha = clamp(1.0 - reveal,0.0,1.0);
                FragColor = vec4(avgColor,alpha);  
                //FragColor = vec4(avgColor.rgb ,alpha);
            }
        ";

        public override void Draw()
        {

        }

        public int UseShader()
        {
            GL.UseProgram(_program);
            return _program;
        }
    }
}
