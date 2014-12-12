#If WITH_REPLICATION Then
Imports System.Threading
Imports System.Net
Imports System.Net.Sockets
Imports Volante
Namespace Volante.Impl

	''' <summary>
	''' File performing asynchronous replication of changed pages to specified slave nodes.
	''' </summary>
	Public Class AsyncReplicationMasterFile
		Inherits ReplicationMasterFile
		''' <summary>
		''' Constructor of replication master file
		''' <param name="db">replication database</param>
		''' <param name="file">local file used to store data locally</param>
		''' <param name="asyncBufSize">size of asynchronous buffer</param>
		''' </summary>
		Public Sub New(db As ReplicationMasterDatabaseImpl, file As IFile, asyncBufSize As Integer)
			MyBase.New(db, file)
			Me.asyncBufSize = asyncBufSize
			start()
		End Sub

		''' <summary>
		''' Constructor of replication master file
		''' <param name="file">local file used to store data locally</param>
		''' <param name="hosts">slave node hosts to which replication will be performed</param>
		''' <param name="asyncBufSize">size of asynchronous buffer</param>
		''' <param name="ack">whether master should wait acknowledgment from slave node during trasanction commit</param>
		''' </summary>
		Public Sub New(file As IFile, hosts As [String](), asyncBufSize As Integer, ack As Boolean)
			MyBase.New(file, hosts, ack)
			Me.asyncBufSize = asyncBufSize
			start()
		End Sub

		Private Sub start()
			go = New Object()
			async = New Object()
			thread = New Thread(New ThreadStart(AddressOf run))
			thread.Start()
		End Sub

		Private Class Parcel
			Public data As Byte()
			Public pos As Long
			Public host As Integer
			Public [next] As Parcel
		End Class

		Public Overrides Sub Write(pos As Long, buf As Byte())
			file.Write(pos, buf)
			For i As Integer = 0 To sockets.Length - 1
				If sockets(i) IsNot Nothing Then
					Dim data As Byte() = New Byte(8 + (buf.Length - 1)) {}
					Bytes.pack8(data, 0, pos)
					Array.Copy(buf, 0, data, 8, buf.Length)
					Dim p As New Parcel()
					p.data = data
					p.pos = pos
					p.host = i

					SyncLock async
						buffered += data.Length
						While buffered > asyncBufSize
							Monitor.Wait(async)
						End While
					End SyncLock

					SyncLock go
						If head Is Nothing Then
							head = InlineAssignHelper(tail, p)
						Else
							tail = InlineAssignHelper(tail.[next], p)
						End If
						Monitor.Pulse(go)
					End SyncLock
				End If
			Next
		End Sub

		Public Sub run()
			While True
				Dim p As Parcel
				SyncLock go
					While head Is Nothing
						If closed Then
							Return
						End If
						Monitor.Wait(go)
					End While
					p = head
					head = p.[next]
				End SyncLock

				SyncLock async
					If buffered > asyncBufSize Then
						Monitor.PulseAll(async)
					End If
					buffered -= p.data.Length
				End SyncLock
				Dim i As Integer = p.host
				While sockets(i) IsNot Nothing
					Try
						sockets(i).Send(p.data)
						If Not ack OrElse p.pos <> 0 OrElse sockets(i).Receive(rcBuf) = 1 Then
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
			End While
		End Sub

		Public Overrides Sub Close()
			SyncLock go
				closed = True
				Monitor.Pulse(go)
			End SyncLock
			thread.Join()
			MyBase.Close()
		End Sub

		Private asyncBufSize As Integer
		Private buffered As Integer
		Private closed As Boolean
		Private go As Object
		Private async As Object
		Private head As Parcel
		Private tail As Parcel
		Private thread As Thread
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
#End If
