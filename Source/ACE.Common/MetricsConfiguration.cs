using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Common
{
    public class MetricsConfiguration
    {
        public bool Enabled { get; set; } = false;
        public string Endpoint { get; set; } = "";
        public string InstanceId { get; set; } = "";
        public string ApiToken { get; set; } = "";
    }
}
