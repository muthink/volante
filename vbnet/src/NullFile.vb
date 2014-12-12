Namespace Volante
	''' <summary>
	''' This implementation of <code>IFile</code> interface can be used
	''' to make Volante as an main-memory database. It should be used when
	''' cacheSizeInBytes is set to <code>0</code>.
	''' In this case all pages are cached in memory and <code>NullFile</code>
	''' is used just as a stub.
	''' <code>NullFile</code> should be used only when data is transient
	''' i.e. it will not be saved between database sessions. If you need
	''' an in-memory database that provides data persistency, 
	''' you should use normal file and infinite page pool size. 
	''' </summary>
	Public Class NullFile
		Implements IFile
        Public Property Listener() As FileListener Implements IFile.Listener
            Get
                Return m_Listener
            End Get
            Set(value As FileListener)
                m_Listener = Value
            End Set
        End Property
		Private m_Listener As FileListener

        Public Sub Write(pos As Long, buf As Byte()) Implements IFile.Write
            If Listener IsNot Nothing Then
                Listener.OnWrite(pos, buf.Length)
            End If
        End Sub

        Public Function Read(pos As Long, buf As Byte()) As Integer Implements IFile.Read
            If Listener IsNot Nothing Then
                Listener.OnRead(pos, buf.Length, 0)
            End If
            Return 0
        End Function

        Public Sub Sync() Implements IFile.Sync
            If Listener IsNot Nothing Then
                Listener.OnSync()
            End If
        End Sub

        Public Sub Lock() Implements IFile.Lock
        End Sub

        Public Sub Close() Implements IFile.Close
        End Sub

        Public Property NoFlush() As Boolean Implements IFile.NoFlush
            Get
                Return False
            End Get
            Set(value As Boolean)
            End Set
        End Property

        Public ReadOnly Property Length() As Long Implements IFile.Length
            Get
                Return 0
            End Get
        End Property
	End Class
End Namespace
