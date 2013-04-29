using CustomNetworking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace StringSocketTester
{


    /// <summary>
    ///This is a test class for StringSocketTest and is intended
    ///to contain all StringSocketTest Unit Tests
    ///</summary>
    [TestClass()]
    public class StringSocketTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        /// <summary>
        ///A simple test for BeginSend and BeginReceive
        ///</summary>
        [TestMethod()]
        public void Test1()
        {
            new Test1Class().run(4001);
        }

        public class Test1Class
        {
            // Data that is shared across threads
            private ManualResetEvent mre1;
            private ManualResetEvent mre2;
            private String s1;
            private object p1;
            private String s2;
            private object p2;

            // Timeout used in test case
            private static int timeout = 2000;

            public void run(int port)
            {
                // Create and start a server and client.
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // This will coordinate communication between the threads of the test cases
                    mre1 = new ManualResetEvent(false);
                    mre2 = new ManualResetEvent(false);

                    // Make two receive requests
                    receiveSocket.BeginReceive(CompletedReceive1, 1);
                    receiveSocket.BeginReceive(CompletedReceive2, 2);

                    // Now send the data.  Hope those receive requests didn't block!
                    String msg = "Hello world\nThis is a test\n";
                    //foreach (char c in msg)
                    //{
                    //    sendSocket.BeginSend(c.ToString(), (e, o) => { }, null);
                    //}
                    sendSocket.BeginSend(msg, (e, o) => { }, null); 

                    // Make sure the lines were received properly.
                    Assert.AreEqual(true, mre1.WaitOne(timeout), "Timed out waiting 1");
                    Assert.AreEqual("Hello world", s1);
                    Assert.AreEqual(1, p1);

                    Assert.AreEqual(true, mre2.WaitOne(timeout), "Timed out waiting 2");
                    Assert.AreEqual("This is a test", s2);
                    Assert.AreEqual(2, p2);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // This is the callback for the first receive request.  We can't make assertions anywhere
            // but the main thread, so we write the values to member variables so they can be tested
            // on the main thread.
            private void CompletedReceive1(String s, Exception o, object payload)
            {
                s1 = s;
                p1 = payload;
                mre1.Set();
            }

            // This is the callback for the second receive request.
            private void CompletedReceive2(String s, Exception o, object payload)
            {
                s2 = s;
                p2 = payload;
                mre2.Set();
            }
        }


        /// <summary>
        /// Puts two BeginReceives into action, then sends two lines, one character at
        /// a time.  Makes sure that the two strings arrive in the right order.
        /// </summary>
        [TestMethod()]
        public void Test2()
        {
            new Test2Class().run(4002);
        }

        public class Test2Class
        {
            public void run(int port)
            {
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    // Create and start a server and client
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // Set up the events
                    e1 = new ManualResetEvent(false);
                    e2 = new ManualResetEvent(false);
                    e3 = new ManualResetEvent(false);

                    // Ask for two lines of text.
                    receiveSocket.BeginReceive(Callback1, null);
                    receiveSocket.BeginReceive(Callback2, null);

                    // Send some text, one character at a time
                    foreach (char c in message1 + "\n" + message2 + "\n")
                    {
                        sendSocket.BeginSend(c.ToString(), SendCallback, null);
                    }

                    // Make sure everything happened properly.
                    Assert.IsTrue(e1.WaitOne(2000));
                    Assert.IsTrue(e2.WaitOne(2000));
                    Assert.IsTrue(e3.WaitOne(2000));

                    Assert.AreEqual(message1, string1);
                    Assert.AreEqual(message2, string2);
                    Assert.AreEqual(message1.Length + message2.Length + 2, count);
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }
            }

            // The two strings that are sent
            private String message1 = "This is a test";
            private String message2 = "This is another test";

            // The two strings that are received
            private String string1;
            private String string2;

            // Number of times the SendCallback is called
            private int count;

            // For coordinating parallel activity
            private ManualResetEvent e1;
            private ManualResetEvent e2;
            private ManualResetEvent e3;

            // Callback for the first receive
            public void Callback1(String s, Exception e, object payload)
            {
                string1 = s;
                e1.Set();
            }

            // Callback for the second receive
            public void Callback2(String s, Exception e, object payload)
            {
                string2 = s;
                e2.Set();
            }

            // Callback for the send
            public void SendCallback(Exception e, object payload)
            {
                lock (e3)
                {
                    count++;
                }
                if (count == message1.Length + message2.Length + 2)
                {
                    e3.Set();
                }
            }

        }

        /// <summary>
        /// Use two threads to send threads to a single receiver, then makes sure that
        /// they arrived in the proper order.
        /// </summary>
        [TestMethod()]
        public void Test3()
        {
            new Test3Class().run(4003);
        }

        public class Test3Class
        {
            // Received strings are collected here
            private List<String> strings = new List<String>();

            // For serializing the executions of BeginReceive
            private ManualResetEvent mre = new ManualResetEvent(false);

            // Number of strings to send per thread
            private static int COUNT = 10000;

            // Timeout for the ManualResetEvent
            private static int TIMEOUT = 5000;

            public void run(int port)
            {
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    // Create and start a server and client
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Make sure we're connected.
                    Assert.IsTrue(serverSocket.Connected);
                    Assert.IsTrue(clientSocket.Connected);

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // Use two threads to blast strings into the socket
                    Thread t1 = new Thread(() => Blast1(sendSocket));
                    Thread t2 = new Thread(() => Blast2(sendSocket));
                    t1.IsBackground = true;
                    t2.IsBackground = true;
                    t1.Start();
                    t2.Start();

                    // Receive all those messages
                    for (int i = 0; i < 2 * COUNT; i++)
                    {
                        receiveSocket.BeginReceive(Receiver, i);
                        Assert.IsTrue(mre.WaitOne(TIMEOUT), "Didn't receive in timely fashion " + i);
                        mre.Reset();
                    }

                    // Make sure that the strings arrived in the proper order
                    int blast1Count = 0;
                    int blast2Count = 0;

                    foreach (String s in strings)
                    {
                        if (s.StartsWith("Blast1"))
                        {
                            Assert.AreEqual(blast1Count, Int32.Parse(s.Substring(7)), "Bad Blast1: " + s);
                            blast1Count++;
                        }
                        else if (s.StartsWith("Blast2"))
                        {
                            Assert.AreEqual(blast2Count, Int32.Parse(s.Substring(7)), "Bad Blast2: " + s);
                            blast2Count++;
                        }
                        else
                        {
                            Assert.Fail("Bad string: " + s);
                        }
                    }
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }

            }

            /// <summary>
            /// Sends COUNT strings that begin with "Blast1" followed by a number.
            /// </summary>
            public void Blast1(StringSocket s)
            {
                for (int i = 0; i < COUNT; i++)
                {
                    s.BeginSend("Blast1 " + i + "\n", (e, p) => { }, null);
                }
            }

            /// <summary>
            /// Sends COUNT strings that begin with "Blast2" followed by a number.
            /// </summary>
            public void Blast2(StringSocket s)
            {
                for (int i = 0; i < COUNT; i++)
                {
                    s.BeginSend("Blast2 " + i + "\n", (e, p) => { }, null);
                }
            }

            /// <summary>
            /// The callback for receiving strings.  Adds to the strings list
            /// and signals.
            /// </summary>
            public void Receiver(String s, Exception e, object payload)
            {
                lock (strings)
                {
                    strings.Add(s);
                    mre.Set();
                }
            }

        }


        /// <summary>
        /// This test fires off multiple senders and receivers, all running on different
        /// threads.  It makes sure that the strings that are send out are all received.
        /// </summary>
        [TestMethod()]
        public void Test4()
        {
            new Test4Class().run(4004);
        }

        public class Test4Class
        {
            private int VERSIONS = 10;         // Number of sending threads; number of receiving threads
            private int COUNT = 5000;          // Number of strings sent/received per thread

            // Collects strings received by receiving threads.
            private List<string> received = new List<string>();

            public void run(int port)
            {
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    // Create and start a server and client
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  
                    // We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Make sure we're connected.
                    Assert.IsTrue(serverSocket.Connected);
                    Assert.IsTrue(clientSocket.Connected);

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // Create a bunch of threads to write to and read from the socket
                    Thread[] threads = new Thread[VERSIONS * 2];
                    for (int i = 0; i < 2 * VERSIONS; i += 2)
                    {
                        threads[i] = new Thread(new ParameterizedThreadStart(v => Sender((int)v, sendSocket)));
                        threads[i + 1] = new Thread(new ParameterizedThreadStart(v => Receiver(receiveSocket)));
                    }

                    // Launch all the threads
                    for (int i = 0; i < threads.Length; i++)
                    {
                        threads[i].IsBackground = true;
                        threads[i].Start(i / 2);
                    }

                    // Wait for all the threads to finish
                    for (int i = 0; i < threads.Length; i++)
                    {
                        threads[i].Join();
                    }

                    // Make sure everything came through properly.  This is where all the
                    // correctness assertions are.
                    received.Sort();
                    for (int v = 0; v < VERSIONS; v++)
                    {
                        string expected = "";
                        char c = (char)('A' + v);
                        for (int i = 0; i < COUNT; i++)
                        {
                            expected += c;
                            Assert.AreEqual(expected, received[v * COUNT + i]);
                        }
                    }
                }
                finally
                {
                    server.Stop();
                    client.Close();
                }

            }

            /// <summary>
            /// Sends strings consisting of the character 'A' + version.  Strings are sent in
            /// the pattern A, AA, AAA, and so on.  COUNT such strings are sent.
            /// </summary>
            private void Sender(int version, StringSocket socket)
            {
                // Determine what character to use
                char c = (char)('A' + version);

                int count = 0;     // Number if times callback has been called
                String msg = "";   // String to send

                // Sent COUNT strings
                for (int i = 0; i < COUNT; i++)
                {
                    // Make the message one character longer
                    msg += c;

                    // Send the message.  The callback atomically updates count.
                    socket.BeginSend(msg + "\n", (e, p) => { Interlocked.Increment(ref count); }, null);
                }

                // Wait until all of the callbacks have been called
                SpinWait.SpinUntil(() => count == COUNT);
            }

            /// <summary>
            /// Receives COUNT strings and appends them to the received list.
            /// </summary>
            private void Receiver(StringSocket socket)
            {
                int count = 0;    // Number of times callback has been called

                // Receive COUNT strings
                for (int i = 0; i < COUNT; i++)
                {
                    // Receive one string.  The callback appends to the list and updates count
                    socket.BeginReceive((s, e, p) => { lock (received) { received.Add(s); count++; } }, i);
                }

                // Wait until all of the callbacks have been called.
                SpinWait.SpinUntil(() => count == COUNT);
            }

        }

        /// <summary>
        /// This test makes sure that StringSockets exhibit the proper non-blocking behavior.
        /// </summary>
        [TestMethod()]
        public void Test5()
        {
            new Test5Class().run(4005);
        }

        public class Test5Class
        {
            public void run(int port)
            {
                TcpListener server = null;
                TcpClient client = null;

                try
                {
                    // Create and start a server and client
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();
                    client = new TcpClient("localhost", port);

                    // Obtain the sockets from the two ends of the connection.  
                    // We are using the blocking AcceptSocket()
                    // method here, which is OK for a test case.
                    Socket serverSocket = server.AcceptSocket();
                    Socket clientSocket = client.Client;

                    // Make sure we're connected.
                    Assert.IsTrue(serverSocket.Connected);
                    Assert.IsTrue(clientSocket.Connected);

                    // Wrap the two ends of the connection into StringSockets
                    StringSocket sendSocket = new StringSocket(serverSocket, new UTF8Encoding());
                    StringSocket receiveSocket = new StringSocket(clientSocket, new UTF8Encoding());

                    // We use this Stopwatch to detect blocking behavior
                    Stopwatch watch = new Stopwatch();
                    watch.Start();

                    // Coordinate the test cases
                    ManualResetEvent mre1 = new ManualResetEvent(false);
                    ManualResetEvent mre2 = new ManualResetEvent(false);
                    ManualResetEvent mre3 = new ManualResetEvent(false);


                    //************************************** TEST1 *********************************************

                    // Make sure that the call to BeginReceive does not block and that its callback runs after the call to BeginSend.
                    long sendTime = 0;
                    long receiveTime = 0;
                    receiveSocket.BeginReceive((s, e, p) => { receiveTime = watch.ElapsedTicks; mre1.Set(); }, null);
                    sendTime = watch.ElapsedTicks;
                    sendSocket.BeginSend("Hello\n", (e, p) => { mre2.Set(); }, null);
                    mre1.WaitOne();
                    mre2.WaitOne();

                    // Make sure things happened in the expected order
                    Assert.IsTrue(sendTime < receiveTime, "Receive happened before send");


                    //************************************** TEST2 *********************************************

                    // Make sure that a blocked receive callback does not block the socket
                    mre1.Reset();
                    mre2.Reset();
                    sendSocket.BeginSend("Hello\n", (e, p) => { }, null);
                    receiveSocket.BeginReceive((s, e, p) => { mre2.Set(); mre1.WaitOne(); mre3.Set(); }, null);
                    mre2.WaitOne();
                    mre2.Reset();
                    sendSocket.BeginSend("there\n", (e, p) => { }, null);
                    receiveSocket.BeginReceive((s, e, p) => { mre2.Set(); }, null);
                    mre2.WaitOne();
                    mre1.Set();
                    mre3.WaitOne();


                    //************************************** TEST3 *********************************************

                    // Make sure that a blocked send callback does not block the socket
                    mre1.Reset();
                    mre2.Reset();
                    mre3.Reset();
                    sendSocket.BeginSend("Hello\n", (e, p) => { mre2.Set(); mre1.WaitOne(); mre3.Set(); }, null);
                    mre2.WaitOne();
                    mre2.Reset();
                    sendSocket.BeginSend("there\n", (e, p) => { mre2.Set(); }, null);
                    mre2.WaitOne();
                    mre1.Set();
                    mre3.WaitOne();


                    //************************************** TEST4 *********************************************

                    // Make sure that one call to BeginReceive cannot block another
                    mre1.Reset();
                    mre2.Reset();
                    mre3.Reset();
                    receiveSocket.BeginReceive((s, e, p) => { mre1.Set(); }, null);
                    receiveSocket.BeginReceive((s, e, p) => { mre2.Set(); }, null);
                    sendSocket.BeginSend("Hello\nthere\n", (e, p) => { mre3.Set(); }, null);
                    mre1.WaitOne();
                    mre2.WaitOne();
                    mre3.WaitOne();

                }
                finally
                {
                    server.Stop();
                    client.Close();
                }

            }
        }

    }
}
