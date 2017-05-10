using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Exceptions
{
    public class CustomException_MapperConfig : System.Exception
    {
        public CustomException_MapperConfig() : base() { }

        public CustomException_MapperConfig(string message) : base(message) { }

        public CustomException_MapperConfig(string message, Exception innerException) : base(message, innerException) { }

        public CustomException_MapperConfig(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
    public class CustomException_DapperMapper : System.Exception
    {
        public CustomException_DapperMapper() : base() { }

        public CustomException_DapperMapper(string message) : base(message) { }

        public CustomException_DapperMapper(string message, Exception innerException) : base(message, innerException) { }

        public CustomException_DapperMapper(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
    public class CustomException_Parser : System.Exception
    {
        public CustomException_Parser() : base() { }

        public CustomException_Parser(string message) : base(message) { }

        public CustomException_Parser(string message, Exception innerException) : base(message, innerException) { }

        public CustomException_Parser(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
