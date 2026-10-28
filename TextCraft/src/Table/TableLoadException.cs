using System;

namespace TextCraft.src.Table
{
    /// <summary>
    /// 数据表（模型 / 方块）加载失败时抛出的异常。
    /// 与普通异常相比，消息中会带上出错的文件、方块或模型名，便于直接定位资源问题。
    /// </summary>
    public sealed class TableLoadException : Exception
    {
        public TableLoadException(string message) : base(message)
        {
        }

        public TableLoadException(string message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
