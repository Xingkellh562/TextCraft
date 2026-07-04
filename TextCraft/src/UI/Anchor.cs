using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.UI
{
    public enum AnchorPoint
    {
        None,      //没有任何锚定点
        Left,      //UpperLeft----Top------UpperRight
        Right,     //  |           |           |
        Top,       //  |           |           |
        Down,      // Left-------Center------Right
        UpperLeft, //  |           |           |
        UpperRight,//  |           |           |
        LowerLeft, //LowerLeft----Down-----LowerRight
        LowerRight,
        Center
    }
    public class Anchor
    {
        public static void GetPosWithAnchor(UIComponent component)
        {
            if (component.parent == null) return;

            Vector2i parentSize = component.parent.rect.size;
            Vector2i posA = component.rect.posA + component.parent.rect.posA;
            Vector2i posB = component.rect.posB + component.parent.rect.posB;

            bool hasA = component.rect.UpperLeftPoint != AnchorPoint.None;
            bool hasB = component.rect.LowerRightPoint != AnchorPoint.None;

            Vector2i offsetA = GetAnchorOffset(component.rect.UpperLeftPoint, parentSize);
            Vector2i offsetB = GetAnchorOffset(component.rect.LowerRightPoint, parentSize);

            if (hasA && hasB)
            {
                posA += offsetA;
                posB += offsetB;
                //(这里TMD搞了好久你信不信)
            }
            else if (hasA && !hasB)
            {
                posA += offsetA;
                posB = posA + component.rect.size;
            }
            else if (!hasA && hasB)
            {
                posB += offsetB;
                posA = posB - component.rect.size;
            }

            component.rect.size = posB - posA;
            if(component is SpiritComponent spiritComponent)
            {
                int[] spirit = spiritComponent.spirit ?? new int[4];
                spiritComponent.GetVertices(posA, posB, spirit);
            }
            else if (component is TextComponent text)
            {
                text.ComposeAndGetTextMesh(posA,posB);
            }

        }

        // 辅助方法：根据锚点类型返回相对于父容器的偏移量
        private static Vector2i GetAnchorOffset(AnchorPoint anchor, Vector2i parentSize)
        {
            switch (anchor)
            {
                case AnchorPoint.Left: return new Vector2i(0, parentSize.Y / 2);
                case AnchorPoint.Right: return new Vector2i(parentSize.X, parentSize.Y / 2);
                case AnchorPoint.Top: return new Vector2i(parentSize.X / 2, 0);
                case AnchorPoint.Down: return new Vector2i(parentSize.X / 2, parentSize.Y);
                case AnchorPoint.Center: return new Vector2i(parentSize.X / 2, parentSize.Y / 2);
                case AnchorPoint.UpperRight: return new Vector2i(parentSize.X, 0);
                case AnchorPoint.LowerLeft: return new Vector2i(0, parentSize.Y);
                case AnchorPoint.LowerRight: return new Vector2i(parentSize.X, parentSize.Y);
                case AnchorPoint.None:
                default: return Vector2i.Zero;
            }
        }
    }
}
