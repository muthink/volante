Namespace Volante
	''' <summary>Allows getting notifications for IFile.Write(), IFile.Read() and
	''' IFile.Sync() calls. Useful as a debugging/diagnostic tool.
	''' </summary>
	Public MustInherit Class FileListener
		Public Overridable Sub OnWrite(pos As Long, len As Long)
		End Sub
		Public Overridable Sub OnRead(pos As Long, bufSize As Long, read As Long)
		End Sub
		Public Overridable Sub OnSync()
		End Sub
	End Class

	''' <summary>Interface for a database file.
	''' Programmer can provide its own implementation of this interface, adding such features
	''' as support encryption, compression etc.
	''' Implentations should throw DatabaseException exception in case of failure.
	''' </summary>
	Public Interface IFile
		''' <summary>Write data to the file
		''' </summary>
		''' <param name="pos">offset in the file
		''' </param>
		''' <param name="buf">array with data to be writter (size is always equal to database page size)
		''' </param>
		''' 
		Sub Write(pos As Long, buf As Byte())

		''' <summary>Read data from the file
		''' </summary>
		''' <param name="pos">offset in the file
		''' </param>
		''' <param name="buf">array to receive data (size is always equal to database page size)
		''' </param>
		''' <returns>number of bytes read
		''' </returns>
		Function Read(pos As Long, buf As Byte()) As Integer

		''' <summary>Flush all file changes to disk
		''' </summary>
		Sub Sync()

		''' <summary>
		''' Prevent other processes from modifying the file
		''' </summary>
		Sub Lock()

		''' <summary>Close the file
		''' </summary>
		Sub Close()

		''' <summary>
		''' Set to <code>true</code> to avoid flushing the stream, or <c>false</c> to flush the stream with every call to <see cref="Sync"/>. Default value is <code>false</code>.
		''' </summary>
		Property NoFlush() As Boolean

		''' <summary>
		''' Length of the file
		''' </summary>
		''' <returns>length of file in bytes</returns>
		ReadOnly Property Length() As Long

		''' <summary>
		''' Get/set <code>IFileMonitor</code> object
		''' </summary>
		Property Listener() As FileListener
	End Interface
End Namespace
