using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace TextCraft.src.Rendering.UI
{
    /// <summary>
    /// UI 专用着色器：顶点色 + 单张纹理 + 可选的"alpha 遮罩"采样。
    /// 与旧版 UIShader 的差异：
    /// <list type="bullet">
    /// <item>顶点属性新增 vec4 颜色，颜色/透明度不再依赖每帧设置的 uniform，因此不同颜色的控件可以合批；</item>
    /// <item>不再为每个控件设置 <c>pictureSize</c>（UV 在 CPU 侧就归一化好），uniform 数量恒定；</item>
    /// <item>不再派生自 BaseShader 的 <c>Draw()</c>：绘制由 <see cref="UIBatchRenderer"/> 按批次区间发起。</item>
    /// </list>
    /// </summary>
    internal sealed class UIShader
    {
        private int _program;
        private int _projectionLocation = -1;
        private int _textureLocation = -1;
        private int _alphaMaskLocation = -1;

        public int Program => _program;

        private const string VertexShaderSource = @"
            #version 450 core
            layout(location = 0) in vec2 aPos;
            layout(location = 1) in vec2 aTexCoord;
            layout(location = 2) in vec4 aColor;

            out vec2 TexCoord;
            out vec4 VertexColor;

            uniform mat4 projection;

            void main()
            {
                gl_Position = projection * vec4(aPos, 0.0, 1.0);
                TexCoord = aTexCoord;
                VertexColor = aColor;
            }";

        private const string FragmentShaderSource = @"
            #version 450 core
            in vec2 TexCoord;
            in vec4 VertexColor;
            out vec4 FragColor;

            uniform sampler2D uiTexture;
            uniform int uAlphaMask;

            void main()
            {
                vec4 texel = texture(uiTexture, TexCoord);
                if (uAlphaMask == 1)
                    FragColor = vec4(VertexColor.rgb, VertexColor.a * texel.a);
                else
                    FragColor = texel * VertexColor;
            }";

        private Matrix4 _projection;

        public void Create()
        {
            if (_program != 0) return;

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, VertexShaderSource);
            GL.CompileShader(vertexShader);
            CheckShader(vertexShader, "VERTEX", VertexShaderSource);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, FragmentShaderSource);
            GL.CompileShader(fragmentShader);
            CheckShader(fragmentShader, "FRAGMENT", FragmentShaderSource);

            _program = GL.CreateProgram();
            GL.AttachShader(_program, vertexShader);
            GL.AttachShader(_program, fragmentShader);
            GL.LinkProgram(_program);

            GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0)
            {
                string log = GL.GetProgramInfoLog(_program);
                throw new Exception($"UIShader 链接失败:\n{log}");
            }

            GL.DetachShader(_program, vertexShader);
            GL.DetachShader(_program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            _projectionLocation = GL.GetUniformLocation(_program, "projection");
            _textureLocation = GL.GetUniformLocation(_program, "uiTexture");
            _alphaMaskLocation = GL.GetUniformLocation(_program, "uAlphaMask");

            GL.UseProgram(_program);
            GL.Uniform1(_textureLocation, 0);
            GL.Uniform1(_alphaMaskLocation, 0);
        }

        private static void CheckShader(int shader, string type, string source)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                throw new Exception($"UIShader 编译失败({type}):\n{log}\n--- 源码 ---\n{source}");
            }
        }

        public void Use() => GL.UseProgram(_program);

        /// <summary>屏幕像素坐标 → 正交投影（左上角原点，Y 向下）。</summary>
        public void SetProjection(Vector2i size)
        {
            Matrix4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, -1, 1, out _projection);
            GL.UniformMatrix4(_projectionLocation, false, ref _projection);
        }

        public void SetAlphaMask(bool alphaMask)
            => GL.Uniform1(_alphaMaskLocation, alphaMask ? 1 : 0);

        /// <summary>
        /// 把材质相关的 uniform 复位到已知状态。
        /// 必须在每帧开始调用：渲染器内部缓存了"当前已设置的材质"，
        /// 如果只重置缓存而不重置 GL 状态，上一帧最后一批（通常是字体）留下的
        /// <c>uAlphaMask=1</c> 会被下一帧第一批继承，导致普通精灵被画成纯白。
        /// </summary>
        public void ResetMaterialState()
        {
            GL.UseProgram(_program);
            GL.Uniform1(_alphaMaskLocation, 0);
            GL.Uniform1(_textureLocation, 0);
        }

        public void BindTexture(Texture texture, TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, texture.Handle);
        }
    }
}
