using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OverlayClipboard
{
    public class Constants
    {
        public static List<int> DefaultAreaScanDownKeys
        {
            get
            {
                return new List<int>() { 17 }; // CTRL + C (vk 17 + 67)
            }
        }
    }
}
