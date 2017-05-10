using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptions
{
    public class CustomException_QueryBuilder : System.Exception
    {
        public CustomException_QueryBuilder() : base() { }

        public CustomException_QueryBuilder(string message) : base(message) { }

        public CustomException_QueryBuilder(string message, Exception innerException) : base(message, innerException) { }

        //public CustomException_Mapper(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    public class CustomException_StringSQLBuilder : System.Exception
    {
        public CustomException_StringSQLBuilder() : base() { }

        public CustomException_StringSQLBuilder(string message) : base(message) { }

        public CustomException_StringSQLBuilder(string message, Exception innerException) : base(message, innerException) { }

        //public CustomException_Mapper(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
