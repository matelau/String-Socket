using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace CustomNetworking
{
    /// <summary> 
    /// A StringSocket is a wrapper around a Socket.  It provides methods that
    /// asynchronously read lines of text (strings terminated by newlines) and 
    /// write strings. (As opposed to Sockets, which read and write raw bytes.)  
    ///
    /// StringSockets are thread safe.  This means that two or more threads may
    /// invoke methods on a shared StringSocket without restriction.  The
    /// StringSocket takes care of the synchonization.
    /// 
    /// Each StringSocket contains a Socket object that is provided by the client.  
    /// A StringSocket will work properly only if the client refains from calling
    /// the contained Socket's read and write methods.
    /// 
    /// If we have an open Socket s, we can create a StringSocket by doing
    /// 
    ///    StringSocket ss = new StringSocket(s, new UTF8Encoding());
    /// 
    /// We can write a string to the StringSocket by doing
    /// 
    ///    ss.BeginSend("Hello world", callback, payload);
    ///    
    /// where callback is a SendCallback (see below) and payload is an arbitrary object.
    /// This is a non-blocking, asynchronous operation.  When the StringSocket has 
    /// successfully written the string to the underlying Socket, or failed in the 
    /// attempt, it invokes the callback.  The parameters to the callback are a
    /// (possibly null) Exception and the payload.  If the Exception is non-null, it is
    /// the Exception that caused the send attempt to fail.
    /// 
    /// We can read a string from the StringSocket by doing
    /// 
    ///     ss.BeginReceive(callback, payload)
    ///     
    /// where callback is a ReceiveCallback (see below) and payload is an arbitrary object.
    /// This is non-blocking, asynchronous operation.  When the StringSocket has read a
    /// string of text terminated by a newline character from the underlying Socket, or
    /// failed in the attempt, it invokes the callback.  The parameters to the callback are
    /// a (possibly null) string, a (possibly null) Exception, and the payload.  Either the
    /// string or the Exception will be non-null, but nor both.  If the string is non-null, 
    /// it is the requested string (with the newline removed).  If the Exception is non-null, 
    /// it is the Exception that caused the send attempt to fail.
    /// </summary>

    public class StringSocket
    {

        // The socket through which we communicate with the remote client
        private Socket _socket;

        //Save the encoding
        private Encoding encoding;


        //String used to handle incoming transmissions for beginReceieve
        string incoming;

        //receieveQueue used to hold onto pending receieve requests
        private Queue<Tuple<ReceiveCallback, Object>> receiveQueue;

        private bool receieveIsOngoing = false;

        // used to lock the given operation 
        private readonly object receiveSync = new object();


        //sendQueue used to hold onto pending send requests
        private Queue<Tuple<string, SendCallback, Object>> sendQueue;


        // For synchronizing sends
        private readonly object sendSync = new object();

        // Records whether an asynchronous send attempt is ongoing
        private bool sendIsOngoing = false;




        // These delegates describe the callbacks that are used for sending and receiving strings.
        public delegate void SendCallback(Exception e, object payload);
        public delegate void ReceiveCallback(String s, Exception e, object payload);

        /// <summary>
        /// Creates a StringSocket from a regular Socket, which should already be connected.  
        /// The read and write methods of the regular Socket must not be called after the
        /// StringSocket is created.  Otherwise, the StringSocket will not behave properly.  
        /// The encoding to use to convert between raw bytes and strings is also provided.
        /// </summary>
        public StringSocket(Socket s, Encoding e)
        {
            _socket = s;
            incoming = "";
            encoding = e;
            receiveQueue = new Queue<Tuple<ReceiveCallback, object>>();
            sendQueue = new Queue<Tuple<string, SendCallback, object>>();
            sendIsOngoing = false;
            receieveIsOngoing = false;
        }

        /// <summary>
        /// We can write a string to a StringSocket ss by doing
        /// 
        ///    ss.BeginSend("Hello world", callback, payload);
        ///    
        /// where callback is a SendCallback (see below) and payload is an arbitrary object.
        /// This is a non-blocking, asynchronous operation.  When the StringSocket has 
        /// successfully written the string to the underlying Socket, or failed in the 
        /// attempt, it invokes the callback.  The parameters to the callback are a
        /// (possibly null) Exception and the payload.  If the Exception is non-null, it is
        /// the Exception that caused the send attempt to fail. 
        /// 
        /// This method is non-blocking.  This means that it does not wait until the string
        /// has been sent before returning.  Instead, it arranges for the string to be sent
        /// and then returns.  When the send is completed (at some time in the future), the
        /// callback is called on another thread.
        /// 
        /// This method is thread safe.  This means that multiple threads can call BeginSend
        /// on a shared socket without worrying around synchronization.  The implementation of
        /// BeginSend must take care of synchronization instead.  On a given StringSocket, each
        /// string arriving via a BeginSend method call must be sent (in its entirety) before
        /// a later arriving string can be sent.
        /// </summary>
        public void BeginSend(String s, SendCallback callback, object payload)
        {
            //lock so properties do not get corrupted
            lock (sendSync)
            {
                //add the item to the queue
                sendQueue.Enqueue(new Tuple<string, SendCallback, object>(s, callback, payload));

                //check if the item is sending already
                if (!sendIsOngoing)
                {
                    sendIsOngoing = true;
                    sendBytes();
                }
            }

        }

        /// <summary>
        /// Helper method for begin Send 
        /// </summary>
        private void sendBytes()
        {
            //check if something is in the queue
            if (sendQueue.Count > 0)
            {
                //get the string from the queue
                Tuple<string, SendCallback, object> tupl = sendQueue.Peek();
                // convert string to be sent
                byte[] _buffer = encoding.GetBytes(tupl.Item1);

                //send through the underlying socket
                _socket.BeginSend(_buffer, 0, _buffer.Length, SocketFlags.None, messageSent, _buffer);
            }

            else
            {   // nothing is in the queue 
                sendIsOngoing = false; 
            }
        }


        /// <summary>
        /// Callback for the underlying socket
        /// </summary>
        /// <param name="AR"></param>
        private void messageSent(IAsyncResult AR)
        {
            // Get exclusive access to send mechanism
            lock (sendSync)
            {
                // Find out how many bytes were actually sent
                int bytes = _socket.EndSend(AR);

                // Get the bytes that we attempted to send
                byte[] outgoingBuffer = (byte[])AR.AsyncState;

                if (outgoingBuffer.Length - bytes > 0)
                {

                    _socket.BeginSend(outgoingBuffer, bytes, outgoingBuffer.Length, SocketFlags.None, messageSent, outgoingBuffer);
                }
                else
                {
                    if (sendQueue.Count > 0)
                    {
                        Tuple<String, SendCallback, object> tupl = sendQueue.Dequeue();

                        string attemptedString = tupl.Item1;
                        SendCallback sndCllBck = tupl.Item2;
                        object pyld = tupl.Item3;

                        LaunchSendThread(sndCllBck, pyld); 
                    }

                }
                sendBytes();

            }



        }

        /// <summary>
        /// We can read a string from the StringSocket by doing
        /// 
        ///     ss.BeginReceive(callback, payload)
        ///     
        /// where callback is a ReceiveCallback (see below) and payload is an arbitrary object.
        /// This is non-blocking, asynchronous operation.  When the StringSocket has read a
        /// string of text terminated by a newline character from the underlying Socket, or
        /// failed in the attempt, it invokes the callback.  The parameters to the callback are
        /// a (possibly null) string, a (possibly null) Exception, and the payload.  Either the
        /// string or the Exception will be non-null, but nor both.  If the string is non-null, 
        /// it is the requested string (with the newline removed).  If the Exception is non-null, 
        /// it is the Exception that caused the send attempt to fail.
        /// 
        /// This method is non-blocking.  This means that it does not wait until a line of text
        /// has been received before returning.  Instead, it arranges for a line to be received
        /// and then returns.  When the line is actually received (at some time in the future), the
        /// callback is called on another thread.
        /// 
        /// This method is thread safe.  This means that multiple threads can call BeginReceive
        /// on a shared socket without worrying around synchronization.  The implementation of
        /// BeginReceive must take care of synchronization instead.  On a given StringSocket, each
        /// arriving line of text must be passed to callbacks in the order in which the corresponding
        /// BeginReceive call arrived.
        /// 
        /// Note that it is possible for there to be incoming bytes arriving at the underlying Socket
        /// even when there are no pending callbacks.  StringSocket implementations should refrain
        /// from buffering an unbounded number of incoming bytes beyond what is required to service
        /// the pending callbacks.
        /// </summary>
        public void BeginReceive(ReceiveCallback callback, object payload)
        {
            lock (receiveSync)
            {
                receiveQueue.Enqueue(new Tuple<ReceiveCallback, object>(callback, payload));

                if (!receieveIsOngoing)
                {
                    receieveIsOngoing = true; 
                    getMessage(); 
                }
            }
        }

        /// <summary>
        /// Helper method for BeginReceive
        /// </summary>
        private void getMessage()
        {
            byte[] buffer = new byte[_socket.ReceiveBufferSize];
            if (receiveQueue.Count > 0)
            {
                if (incoming.Contains('\n'))
                {

                    while (incoming.Contains('\n') && receiveQueue.Count > 0)
                    {
                        Tuple<ReceiveCallback, object> tupl = receiveQueue.Dequeue();
                        string line = incoming.Substring(0, incoming.IndexOf("\n"));
                        ReceiveCallback rcvCllBck = tupl.Item1;
                        object pyld = tupl.Item2;
                        LaunchReceiveThread(rcvCllBck, line, pyld);
                        incoming = incoming.Substring(line.Length + 1);
                        if (receiveQueue.Count == 0)
                        {
                            receieveIsOngoing = false;
                            return;
                        }
                    }
                }

                    _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, MessageReceived, buffer);  
            }
            else
            {
                receieveIsOngoing = false;
            }
            
        }

        /// <summary>
        /// Callback for BeginReceive the underlying Socket
        /// </summary>
        /// <param name="result"></param>
        private void MessageReceived(IAsyncResult AR)
        {

            // Get the buffer to which the data was written.
            byte[] buffer = (byte[])(AR.AsyncState);

            // Figure out how many bytes have come in
            int bytes = _socket.EndReceive(AR);

            // Otherwise, decode and display the incoming bytes.  Then request more bytes.
            lock (receiveSync)
            {
                incoming += encoding.GetString(buffer, 0, bytes);

                // Convert the bytes into a string
                while (incoming.Contains('\n') && receiveQueue.Count > 0)
                {
                    Tuple<ReceiveCallback, object> tupl = receiveQueue.Dequeue();
                    string line = incoming.Substring(0, incoming.IndexOf("\n"));
                    ReceiveCallback rcvCllBck = tupl.Item1;
                    object pyld = tupl.Item2;
                    LaunchReceiveThread(rcvCllBck, line, pyld);
                    incoming = incoming.Substring(line.Length+1);
                }

                getMessage(); 

            }
        }

        /// <summary>
        /// Launch a thread for Closure the stringSocket callback will now preserve variables
        /// </summary>
        /// <param name="r"></param>
        /// <param name="s"></param>
        /// <param name="payload"></param>
        private void LaunchReceiveThread(ReceiveCallback r, string s, object payload)
        {
            ThreadPool.QueueUserWorkItem((e)=> r(s,null, payload)); 
        }

        /// <summary>
        /// Launch a thread for Closure the stringSocket callback will now preserve variables
        /// </summary>
        /// <param name="sndCllBck"></param>
        /// <param name="payload"></param>
        private void LaunchSendThread(SendCallback sndCllBck, object payload)
        {
            ThreadPool.QueueUserWorkItem((e) =>sndCllBck(null, payload )); 
        }
    }
}

    
