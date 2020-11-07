using System;

namespace FNNLib.Exceptions {
    public class NotServerException : Exception {
        public NotServerException() { }
        public NotServerException(string message) : base(message) { }
        public NotServerException(string message, Exception innerEx) : base(message, innerEx) { }
    }
}