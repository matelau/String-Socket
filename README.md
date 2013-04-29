String-Socket
=============
 A StringSocket is a wrapper around a Socket.  It provides methods that
 asynchronously read lines of text (strings terminated by newlines) and 
 write strings. (As opposed to Sockets, which read and write raw bytes.)  

StringSockets are thread safe.  This means that two or more threads may
invoke methods on a shared StringSocket without restriction.  The
StringSocket takes care of the synchonization.
 
Each StringSocket contains a Socket object that is provided by the client.  
A StringSocket will work properly only if the client refrains from calling
the contained Socket's read and write methods.
 
