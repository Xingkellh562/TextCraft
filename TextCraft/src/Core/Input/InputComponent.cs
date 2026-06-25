using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextCraft.src.Core.Input
{
    public struct InputComponent
    {
        public float axisX = 0;
        public float axisY = 0;
        public float axisZ = 0;

        public readonly int Scale = 4;

        public float pastMouseX = 400;
        public float pastMouseY = 300;
        public float mouseX;
        public float mouseY;

        public bool destory =false;
        public bool build = false;

        public bool forward = false;
        public bool back = false;
        public bool left = false;
        public bool right = false;
        public bool up = false;
        public bool down = false;

        public int nowBlock = 1;

        public float interval = 0.2f;
        public float timer = 0;


        public InputComponent()
        {
        }
    }
}
