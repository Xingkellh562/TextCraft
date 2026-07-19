using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Rendering.OIT
{
    internal class OITShader : BaseShader
    {
        private Matrix4 _viewMatrix;
        private Vector3d _chunkPosition;
        private Vector3d _cameraPos;

        //顶点着色器
        protected override string VertexShaderSource => @"
            #version 450 core
            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec3 aTexCoord;

            out vec2 TexCoord;
            out float ao;
            out float viewDistance;
            out float depth;

            uniform mat4 projection;
            uniform mat4 view;
            uniform int atlasSize;
            uniform vec3 chunkPos;

            void main()
            {
                vec4 viewPos = view * vec4(aPos + chunkPos, 1.0);
                gl_Position = projection * viewPos;
                depth = gl_Position.z;
                
                viewDistance = sqrt(viewPos.z*viewPos.z+viewPos.y*viewPos.y+viewPos.x*viewPos.x);
                TexCoord = aTexCoord.xy/atlasSize;
                ao = aTexCoord.z;
            }";
        //片段着色器
        protected override string FragmentShaderSource => @"
            #version 450 core
            in vec2 TexCoord;
            in float ao;
            in float viewDistance;
            in float depth;

            layout(location = 0) out vec4 accumColor;
            layout(location = 1) out float revealColor;

            uniform sampler2D ourTexture;
            uniform vec3 fogColor;
            uniform float fogStart;
            uniform float fogEnd;

            float ComputeWeight(float z, float alpha)
            {
                return clamp(pow(1 - z,1.0) * alpha,0.01,3e3);
            }

            void main()
            {
                
                vec4 t = texture(ourTexture,TexCoord);
                vec3 lib = t.rgb * ao;
                
                float fogFactor = clamp((viewDistance - fogStart)/(fogEnd-fogStart),0.0,1.0);
                vec3 finalColor = mix(lib,fogColor,fogFactor);

                float alpha = t.a ;
   
                float z = 20 / (1000.01 - (gl_FragCoord.z * 2.0 - 1.0) * 999.99);
                
                float weight = ComputeWeight(z, alpha);

                accumColor = vec4(finalColor * alpha * weight,weight* alpha);
                revealColor = alpha;
                //accumColor = vec4(vec3(z),1.0);
                //revealColor = 1.0;
            }";

        public void GetMatrix(Vector3d cameraPos, Vector3 cameraDir, Vector2i size)
        {

            GL.UseProgram(_program);
            _cameraPos = cameraPos;
            float aspectRatio = (float)size.X / size.Y;
            _projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f),
                                                                    aspectRatio,
                                                                    0.1f,
                                                                    1000f
                                                                    );
            _viewMatrix = Matrix4.LookAt(Vector3.Zero, cameraDir, Vector3.UnitY);
            int location = GL.GetUniformLocation(_program, "projection");
            GL.UniformMatrix4(location, false, ref _projectionMatrix);

            int location2 = GL.GetUniformLocation(_program, "view");
            GL.UniformMatrix4(location2, false, ref _viewMatrix);

            int location3 = GL.GetUniformLocation(_program, "atlasSize");
            GL.Uniform1(location3, 16);
        }
        public void SetFog(Vector3 fogColor, float start, float end)
        {
            GL.UseProgram(_program);

            int fogColorLoc = GL.GetUniformLocation(_program, "fogColor");
            GL.Uniform3(fogColorLoc, fogColor);
            int fogStartLoc = GL.GetUniformLocation(_program, "fogStart");
            GL.Uniform1(fogStartLoc, start);
            int fogEndLoc = GL.GetUniformLocation(_program, "fogEnd");
            GL.Uniform1(fogEndLoc, end);
        }

        public override void Draw()
        {
            GL.UseProgram(_program);

            atlas.Bind(TextureUnit.Texture0);
            int textureLocation = GL.GetUniformLocation(_program, "ourTexture");
            GL.Uniform1(textureLocation, 0);

            int location4 = GL.GetUniformLocation(_program, "chunkPos");
            Vector3 pos = (Vector3)(_chunkPosition - _cameraPos);
            GL.Uniform3(location4, pos);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount / 6);

        }

        public void GetGrid(Grid g)
        {
            if (!g.isLoad)
                g.GetVertexObject();
            if (!g.isUpdate)
                g.UpdateVertices();

            if (g.vertices == null) g.vertices = new float[0];
            GetVertices(g.vao, g.vbo, g.vertices.Length);
            _chunkPosition = g.Pos;
        }

        public int GetProgram()
        {
            return _program;
        }
    }
}
   