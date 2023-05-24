using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OverlayClipboard.Model
{
    public class MouseEvent : InputEvent
    {
        public MouseEvent()
        {

        }
        public MouseEvent(MouseEventFlag type, int x, int y, int delta)
        {
            Type = type;
            X = x;
            Y = y;
            Delta = delta;
            Timestamp = DateTime.UtcNow;
        }
        public MouseEventFlag Type { get; set; }
        public int Delta { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    [Flags]
    public enum MouseEventFlag
    {
        Absolute = 0x8000,
        HWheel = 0x01000,
        Move = 0x0001,
        MoveNoCoalesce = 0x2000,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp = 0x0040,
        VirtualDesk = 0x4000,
        Wheel = 0x0800,
        XDown = 0x0080,
        XUp = 0x0100
    }
}
