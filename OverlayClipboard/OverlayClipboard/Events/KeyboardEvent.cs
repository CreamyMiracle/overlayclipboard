using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OverlayClipboard.Model
{
    public class KeyboardEvent : InputEvent
    {
        public KeyboardEvent(int virtualCode, KeyEventType type)
        {
            Timestamp = DateTime.UtcNow;
            VirtualKeyCode = virtualCode;
            Type = type;
        }
        public KeyboardEvent()
        {

        }

        public KeyEventType Type { get; set; }

        public int VirtualKeyCode { get; set; }
    }

    [Flags]
    public enum KeyEventType
    {
        KeyDown = 0x0000,
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        Scancode = 0x0008
    }
}
