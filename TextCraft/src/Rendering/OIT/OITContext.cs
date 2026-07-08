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
    internal class OITContext
    {
        public OITShader OitShader { get; set; }
        private CompositeShader _compositeShader;
        private Vector2i _size;
        private int _oitFBO = -1,_oitDepthBuffer = -1;
        private int _accumTex = -1, _revealTex = -1;
        private int _screenVao = -1,_screenVbo = -1;
        public OITContext()
        {
            OitShader = new OITShader();
            OitShader.CreateShaderProgram();
            _compositeShader = new CompositeShader();
            _compositeShader.CreateShaderProgram();
        }

        //重设FBO并创建accumTex与revealTex
        public void ReSize(Vector2i size)
        {
            _size = new Vector2i(Math.Max(1,size.X), Math.Max(1, size.Y));
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            DeleteResources();

            if (_oitFBO == -1)
                _oitFBO = GL.GenFramebuffer();            

            GL.BindFramebuffer(FramebufferTarget.Framebuffer,_oitFBO);

            _oitDepthBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _oitDepthBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent32, _size.X, _size.Y);

            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, _oitDepthBuffer);
            //创建累加纹理
            _accumTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _accumTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _size.X, _size.Y, 0,
                PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _accumTex, 0);



            //创建揭示度纹理
            _revealTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _revealTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, _size.X, _size.Y, 0,
                PixelFormat.Red, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
                TextureTarget.Texture2D, _revealTex, 0);

            //创建与绑定FBO,并指定oitShader输出对象
            GL.DrawBuffers(2,new DrawBuffersEnum[]
            {
                DrawBuffersEnum.ColorAttachment0,
                DrawBuffersEnum.ColorAttachment1
            });

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"OIT FBO重建失败！错误码：{status}");
            }


            GL.BindTexture(TextureTarget.Texture2D,0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer,0);

        }

        public void DeleteResources()
        {
            if (_accumTex != -1) GL.DeleteTexture(_accumTex);
            if (_revealTex != -1) GL.DeleteTexture(_revealTex);
            if (_oitDepthBuffer != -1) GL.DeleteRenderbuffer(_oitDepthBuffer);
        }

        public void GetFullScreen()
        {
            float[] vertices = new float[]
            {
                -1f,-1f,0,0,0,0,
                1f,-1f,0,1,0,0,
                1f,1f,0,1,1,0,
                -1f,-1f,0,0,0,0,
                1f,1f,0,1,1,0,
                -1f,1f,0,0,1,0,
            };

            _screenVao = GL.GenVertexArray();
            GL.BindVertexArray(_screenVao);
            _screenVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _screenVbo);

            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), Vector3.SizeInBytes);
            GL.EnableVertexAttribArray(1);
        }

        public void BeginRenderTransparentGrid()
        {

            GL.BindFramebuffer(FramebufferTarget.Framebuffer,_oitFBO);

            GL.Viewport(0, 0, _size.X, _size.Y);

            GL.ClearBuffer(ClearBuffer.Color, 0, new float[]  { 0, 0, 0, 0 });
            GL.ClearBuffer(ClearBuffer.Color, 1, new float[] { 1, 0, 0, 0 });

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _oitFBO);
            GL.BlitFramebuffer(0,0,_size.X,_size.Y, 0, 0, _size.X, _size.Y,
                                ClearBufferMask.DepthBufferBit,BlitFramebufferFilter.Nearest);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _oitFBO);

            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.DepthTest);
            //GL.Disable(EnableCap.DepthTest);
            //GL.Disable(EnableCap.CullFace);
            GL.DepthMask(false);
            
            GL.Enable(EnableCap.Blend);

            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
            //GL.BlendFuncSeparate(0,BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendFunc(1, BlendingFactorSrc.Zero, BlendingFactorDest.OneMinusSrcColor);
        }
        public void Composite()
        {
            int program = _compositeShader.UseShader();

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, _accumTex);
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, _revealTex);

            int accumTexLocation = GL.GetUniformLocation(program, "accumTex");
            GL.Uniform1(accumTexLocation, 2);
            int revealTexLocation = GL.GetUniformLocation(program, "revealTex");
            GL.Uniform1(revealTexLocation, 3);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(true);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);

            GL.BindVertexArray(_screenVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
    }

}
