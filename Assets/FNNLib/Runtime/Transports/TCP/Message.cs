﻿// Implemented with strong reference to Mirror, so here's the license:
/*
MIT License

Copyright (c) 2015, Unity Technologies
Copyright (c) 2019, vis2k, Paul and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 */

namespace FNNLib.Transports.TCP {
    public enum EventType {
        Connect,
        Data,
        Disconnect
    }
    public struct Message {
        public readonly int clientID;
        public readonly EventType eventType;
        public readonly byte[] data;

        public Message(int clientID, EventType eventType, byte[] data) {
            this.clientID = clientID;
            this.eventType = eventType;
            this.data = data;
        }
    }
}