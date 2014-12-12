Namespace Volante
	''' <summary>
	''' IFile implementation which to store databases on <see cref="System.IO.Stream"/> instances.
	''' </summary>
	Public Class StreamFile
		Implements IFile
		Private offset As Long = 0
		Private stream As System.IO.Stream

		Public Property Listener() As FileListener
			Get
				Return m_Listener
			End Get
			Set
				m_Listener = Value
			End Set
		End Property
		Private m_Listener As FileListener

		''' <summary>
		''' Construction
		''' </summary>
		''' <param name="stream">A <see cref="System.IO.Stream"/> where to store the database</param>
		Public Sub New(stream As System.IO.Stream)
			Me.stream = stream
		End Sub

		''' <summary>
		''' Construction
		''' </summary>
		''' <param name="stream">A <see cref="System.IO.Stream"/> where to store the database</param>
		''' <param name="offset">Offset within the stream where to store/find the database</param>
		Public Sub New(stream As System.IO.Stream, offset As Long)
			Me.stream = stream
			Me.offset = offset
		End Sub

		''' <summary>
		''' Write method
		''' </summary>
		''' <param name="pos">Zero-based position</param>
		''' <param name="buf">Buffer to write to the stream. The entire buffer is written</param>
		Public Sub Write(pos As Long, buf As Byte())
			stream.Position = pos + offset
			stream.Write(buf, 0, buf.Length)
			If Listener IsNot Nothing Then
				Listener.OnWrite(pos, buf.Length)
			End If
		End Sub

		''' <summary>
		''' Read method
		''' </summary>
		''' <param name="pos">Zero-based position</param>
		''' <param name="buf">Buffer where to store <c>buf.Length</c> byte(s) read from the stream</param>
		Public Function Read(pos As Long, buf As Byte()) As Integer
			stream.Position = pos + offset
			Dim len As Integer = stream.Read(buf, 0, buf.Length)
			If Listener IsNot Nothing Then
				Listener.OnRead(pos, buf.Length, len)
			End If
			Return len
		End Function

		''' <summary>
		''' Flushes the stream (subject to the NoFlush property)
		''' </summary>

		Public Sub Sync()
			If NoFlush = False Then
				stream.Flush()
			End If
			If Listener IsNot Nothing Then
				Listener.OnSync()
			End If
		End Sub

		''' <summary>
		''' Closes the stream (subject to the NoFlush property)
		''' </summary>
		Public Sub Close()
			stream.Close()
		End Sub

		''' <summary>
		''' Locks the stream (no-op)
		''' </summary>
		Public Sub Lock()
		End Sub

		''' <summary>
		''' Boolean property. Set to <c>true</c> to avoid flushing the stream, or <c>false</c> to flush the stream with every calls to <see cref="Sync"/>
		''' </summary>
		Public Property NoFlush() As Boolean
			Get
				Return m_NoFlush
			End Get
			Set
				m_NoFlush = Value
			End Set
		End Property
		Private m_NoFlush As Boolean

		Public ReadOnly Property Length() As Long
			Get
				Return stream.Length
			End Get
		End Property
	End Class
End Namespace
