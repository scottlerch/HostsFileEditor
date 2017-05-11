using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace HostsFileEditor.Utilities
{
    public class DnsEx
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern UInt32 DnsFlushResolverCache();

        public static void FlushMyCache()
        {
            UInt32 result = DnsFlushResolverCache();
        }
    }
}
