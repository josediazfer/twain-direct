using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwainDirect.Scanner.Common
{
    public class TwainDirectScannerException : Exception
    {
        public TwainDirectScannerException()
        {
        }

        public TwainDirectScannerException(string message)
            : base(message)
        {
        }

        public TwainDirectScannerException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
