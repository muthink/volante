#If WITH_REPLICATION Then
Imports System.Net
Imports System.Net.Sockets
Imports System.Threading
Imports Volante
Namespace Volante.Impl

	Public Class ReplicationSlaveDatabaseImpl
		Inherits DatabaseImpl
		Inherits ReplicationSlaveDatabase
		Public Sub New(port As Integer)
			Me.port = port
		End Sub

		Public Overrides Sub Open(file As IFile, cacheSizeInBytes As Integer)
			acceptor = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			acceptor.Bind(New IPEndPoint(IPAddress.Any, port))
			acceptor.Listen(ListenQueueSize)
			If file.Length > 0 Then
				Dim rootPage As Byte() = New Byte(Page.pageSize - 1) {}
				Try
					file.Read(0, rootPage)
					prevIndex = rootPage(DB_HDR_CURR_INDEX_OFFSET)
					initialized = rootPage(DB_HDR_INITIALIZED_OFFSET) <> 0
				Catch generatedExceptionName As DatabaseException
					initialized = False
					prevIndex = -1
				End Try
			Else
				prevIndex = -1
				initialized = False
			End If
			Me.file = file
			lck = New PersistentResource()
			init = New Object()
			done = New Object()
			commit = New Object()
			listening = True
			connect()
			pool = New PagePool(cacheSizeInBytes / Page.pageSize)
			pool.open(file)
			thread = New Thread(New ThreadStart(AddressOf run))
			thread.Name = "ReplicationSlaveStorageImpl"
			thread.Start()
			WaitInitializationCompletion()
			MyBase.Open(file, cacheSizeInBytes)
		End Sub


		''' <summary>
		''' Check if socket is connected to the master host
		''' @return <code>true</code> if connection between slave and master is sucessfully established
		''' </summary>
		Public Function IsConnected() As Boolean
			Return socket IsNot Nothing
		End Function

		Public Overrides Sub BeginThreadTransaction(mode As TransactionMode)
			If mode <> TransactionMode.ReplicationSlave Then
				Throw New ArgumentException("Illegal transaction mode")
			End If
			lck.SharedLock()
			Dim pg As Page = pool.getPage(0)
			header.unpack(pg.data)
			pool.unfix(pg)
			currIndex = 1 - header.curr
			currIndexSize = header.root(1 - currIndex).indexUsed
			committedIndexSize = currIndexSize
			usedSize = header.root(currIndex).size
		End Sub

		Public Overrides Sub EndThreadTransaction(maxDelay As Integer)
			lck.Unlock()
		End Sub

		Protected Sub WaitInitializationCompletion()
			SyncLock init
				While Not initialized
					Monitor.Wait(init)
				End While
			End SyncLock
		End Sub

		''' <summary>
		''' Wait until database is modified by master
		''' This method blocks current thread until master node commits trasanction and
		''' this transanction is completely delivered to this slave node
		''' </summary>
		Public Sub WaitForModification()
			SyncLock commit
				If socket IsNot Nothing Then
					Monitor.Wait(commit)
				End If
			End SyncLock
		End Sub

		Const DB_HDR_CURR_INDEX_OFFSET As Integer = 0
		Const DB_HDR_DIRTY_OFFSET As Integer = 1
		Const DB_HDR_INITIALIZED_OFFSET As Integer = 2
		Const PAGE_DATA_OFFSET As Integer = 8

		Public Shared ListenQueueSize As Integer = 10
		Public Shared LingerTime As Integer = 10
		' linger parameter for the socket
		Private Sub connect()
			Try
				socket = acceptor.Accept()
			Catch generatedExceptionName As SocketException
				socket = Nothing
			End Try
		End Sub

		''' <summary>
		''' When overriden by base class this method perfroms socket error handling
		''' @return <code>true</code> if host should be reconnected and attempt to send data to it should be 
		''' repeated, <code>false</code> if no more attmpts to communicate with this host should be performed 
		''' </summary>     
		Public Overridable Function HandleError() As Boolean
			Return If((Listener IsNot Nothing), Listener.ReplicationError(Nothing), False)
		End Function

		Public Sub run()
			Dim buf As Byte() = New Byte(Page.pageSize + (PAGE_DATA_OFFSET - 1)) {}

			While listening
				Dim offs As Integer = 0
				Do
					Dim rc As Integer
					Try
						rc = socket.Receive(buf, offs, buf.Length - offs, SocketFlags.None)
					Catch generatedExceptionName As SocketException
						rc = -1
					End Try
					SyncLock done
						If Not listening Then
							Return
						End If
					End SyncLock
					If rc < 0 Then
						If HandleError() Then
							connect()
						Else
							Return
						End If
					Else
						offs += rc
					End If
				Loop While offs < buf.Length

				Dim pos As Long = Bytes.unpack8(buf, 0)
				Dim transactionCommit As Boolean = False
				If pos = 0 Then
					If replicationAck Then
						Try
							socket.Send(buf, 0, 1, SocketFlags.None)
						Catch generatedExceptionName As SocketException
							HandleError()
						End Try
					End If
					If buf(PAGE_DATA_OFFSET + DB_HDR_CURR_INDEX_OFFSET) <> prevIndex Then
						prevIndex = buf(PAGE_DATA_OFFSET + DB_HDR_CURR_INDEX_OFFSET)
						lck.ExclusiveLock()
						transactionCommit = True
					End If
				ElseIf pos < 0 Then
					SyncLock commit
						hangup()
						Monitor.PulseAll(commit)
					End SyncLock
					Return
				End If

				Dim pg As Page = pool.putPage(pos)
				Array.Copy(buf, PAGE_DATA_OFFSET, pg.data, 0, Page.pageSize)
				pool.unfix(pg)

				If pos = 0 Then
					If Not initialized AndAlso buf(PAGE_DATA_OFFSET + DB_HDR_INITIALIZED_OFFSET) <> 0 Then
						SyncLock init
							initialized = True
							Monitor.Pulse(init)
						End SyncLock
					End If
					If transactionCommit Then
						lck.Unlock()
						SyncLock commit
							Monitor.PulseAll(commit)
						End SyncLock
						pool.flush()
					End If
				End If
			End While
		End Sub

		Public Overrides Sub Close()
			SyncLock done
				listening = False
			End SyncLock
			thread.Interrupt()
			thread.Join()
			hangup()

			pool.flush()
			MyBase.Close()
		End Sub

		Private Sub hangup()
			If socket IsNot Nothing Then
				Try
					socket.Close()
				Catch generatedExceptionName As SocketException
				End Try
				socket = Nothing
			End If
		End Sub

		Protected Overrides Function isDirty() As Boolean
			Return False
		End Function

		Protected socket As Socket
		Protected port As Integer
		Protected file As IFile
		Protected initialized As Boolean
		Protected listening As Boolean
		Protected init As Object
		Protected done As Object
		Protected commit As Object
		Protected prevIndex As Integer
		Protected lck As IResource
		Protected acceptor As Socket
		Protected thread As Thread
	End Class
End Namespace
#End If
