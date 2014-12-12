#If WITH_REPLICATION Then
Imports System.Net.Sockets
Imports System.Net
Imports System.Threading
Imports Volante
Namespace Volante.Impl

	''' <summary>
	''' File performing replication of changed pages to specified slave nodes.
	''' </summary>
	Public Class ReplicationMasterFile
		Implements IFile
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
		''' Constructor of replication master file
		''' </summary>
		''' <param name="db">replication database</param>
		''' <param name="file">local file used to store data locally</param>
		Public Sub New(db As ReplicationMasterDatabaseImpl, file As IFile)
			Me.New(file, db.hosts, db.replicationAck)
			Me.db = db
		End Sub

		''' <summary>
		''' Constructor of replication master file
		''' </summary>
		''' <param name="file">local file used to store data locally</param>
		''' <param name="hosts">slave node hosts to which replication will be performed</param>
		''' <param name="ack">whether master should wait acknowledgment from slave node during trasanction commit</param>
		Public Sub New(file As IFile, hosts As String(), ack As Boolean)
			Me.file = file
			Me.hosts = hosts
			Me.ack = ack
			sockets = New Socket(hosts.Length - 1) {}
			rcBuf = New Byte(0) {}
			txBuf = New Byte(8 + (Page.pageSize - 1)) {}
			nHosts = 0
			For i As Integer = 0 To hosts.Length - 1
				connect(i)
			Next
		End Sub

		Public Function GetNumberOfAvailableHosts() As Integer
			Return nHosts
		End Function

		Protected Sub connect(i As Integer)
			Dim host As [String] = hosts(i)
			Dim colon As Integer = host.IndexOf(":"C)
			Dim port As Integer = Integer.Parse(host.Substring(colon + 1))
			host = host.Substring(0, colon)
			Dim socket As Socket = Nothing
			socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			For j As Integer = 0 To MaxConnectionAttempts - 1
				For Each ip As IPAddress In Dns.GetHostEntry(host).AddressList
					Try
						socket.Connect(New IPEndPoint(ip, port))
						sockets(i) = socket
						nHosts += 1
						socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1)
						socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, New System.Net.Sockets.LingerOption(True, LingerTime))
						Return
					Catch generatedExceptionName As SocketException
					End Try
				Next
				Thread.Sleep(ConnectionTimeout)
			Next
			HandleError(hosts(i))
		End Sub

		''' <summary>
		''' When overriden by base class this method perfroms socket error handling
		''' </summary>     
		''' <returns><code>true</code> if host should be reconnected and attempt to send data to it should be 
		''' repeated, <code>false</code> if no more attmpts to communicate with this host should be performed 
		''' </returns>
		Public Function HandleError(host As String) As Boolean
			Return If((db IsNot Nothing AndAlso db.Listener IsNot Nothing), db.Listener.ReplicationError(host), False)
		End Function

		Public Overridable Sub Write(pos As Long, buf As Byte())
			For i As Integer = 0 To sockets.Length - 1
				While sockets(i) IsNot Nothing
					Try
						Bytes.pack8(txBuf, 0, pos)
						Array.Copy(buf, 0, txBuf, 8, buf.Length)
						sockets(i).Send(txBuf)
						If Not ack OrElse pos <> 0 OrElse sockets(i).Receive(rcBuf) = 1 Then
							Exit Try
						End If
					Catch generatedExceptionName As SocketException
					End Try

					sockets(i) = Nothing
					nHosts -= 1
					If HandleError(hosts(i)) Then
						connect(i)
					Else
						Exit While
					End If
				End While
			Next
			file.Write(pos, buf)
		End Sub

		Public Function Read(pos As Long, buf As Byte()) As Integer
			Return file.Read(pos, buf)
		End Function

		Public Sub Sync()
			file.Sync()
		End Sub

		Public Sub Lock()
			file.Lock()
		End Sub

		Public Property NoFlush() As Boolean
			Get
				Return file.NoFlush
			End Get
			Set
				file.NoFlush = value
			End Set
		End Property

		Public Overridable Sub Close()
			file.Close()
			Bytes.pack8(txBuf, 0, -1)
			For i As Integer = 0 To sockets.Length - 1
				If sockets(i) IsNot Nothing Then
					Try
						sockets(i).Send(txBuf)
						sockets(i).Close()
					Catch generatedExceptionName As SocketException
					End Try
				End If
			Next
		End Sub

		Public ReadOnly Property Length() As Long
			Get
				Return file.Length
			End Get
		End Property

		Public Shared LingerTime As Integer = 10
		' linger parameter for the socket
		Public Shared MaxConnectionAttempts As Integer = 10
		' attempts to establish connection with slave node
		Public Shared ConnectionTimeout As Integer = 1000
		' timeout between attempts to conbbect to the slave
		Protected sockets As Socket()
		Protected txBuf As Byte()
		Protected rcBuf As Byte()
		Protected file As IFile
		Protected hosts As String()
		Protected nHosts As Integer
		Protected ack As Boolean

		Protected db As ReplicationMasterDatabaseImpl
	End Class
End Namespace
#End If
