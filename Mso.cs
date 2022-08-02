using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ABX_Audio_Devices
{
    public class Mso
    {
        public Dictionary<string,Input>? inputs { get; set; }

        public class Input
        {
            public string label { get; set; }
        }
    }
}
