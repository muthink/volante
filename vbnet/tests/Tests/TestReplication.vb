#If WITH_REPLICATION Then
Imports System.Diagnostics
Imports System.Threading
Namespace Volante

	Public Class TestReplication
		Implements ITest
		Private Class Record
			Inherits Persistent
			Public key As Integer
		End Class

		Private log As Boolean = False
		Const count As Integer = 1000
		Const transSize As Integer = 100
		Const defaultPort As Integer = 6000
		Const asyncBufSize As Integer = 1024 * 1024
		Const cacheSizeInBytes As Integer = 32 * 1024 * 1024

		Private slaveCurrKey As Long = 0

		Private Sub Master(port As Integer, async As Boolean, ack As Boolean, nIterations As Integer)
			Console.WriteLine("Starting a replication master")
			Dim db As ReplicationMasterDatabase = DatabaseFactory.CreateReplicationMasterDatabase(New String() {"localhost:" & port}, If(async, asyncBufSize, 0))
			Dim dbName As String = "replicmaster.dbs"
			Tests.TryDeleteFile(dbName)
			db.ReplicationAck = ack
			Dim dbFile = New OsFile(dbName)
			dbFile.NoFlush = True
			db.Open(dbFile, cacheSizeInBytes)

			Dim root As IFieldIndex(Of Integer, Record) = DirectCast(db.Root, IFieldIndex(Of Integer, Record))
			If root Is Nothing Then
				root = db.CreateFieldIndex(Of Integer, Record)("key", IndexType.Unique)
				db.Root = root
			End If
			Dim start As DateTime = DateTime.Now
			Dim i As Integer
			Dim lastKey As Integer = 0
			For i = 0 To nIterations - 1
				If i >= count Then
					root.Remove(New Key(i - count))
				End If

				Dim rec As New Record()
				rec.key = i
				lastKey = rec.key
				root.Put(rec)
				If i >= count AndAlso i Mod transSize = 0 Then
					db.Commit()
				End If
				If log AndAlso i Mod 1000 = 0 Then
					Console.WriteLine("Master processed {0} rounds", i)
				End If
			Next
			db.Commit()
			While True
				Dim slaveKey As Long = Interlocked.Read(slaveCurrKey)
				If slaveKey = lastKey Then
					Exit While
				End If
					' 1/10th sec
				Thread.Sleep(100)
			End While
			db.Close()

			Console.WriteLine("Replication master finished", i)
		End Sub

		Private Sub Slave(port As Integer, async As Boolean, ack As Boolean)
			Console.WriteLine("Starting a replication slave")
			Dim i As Integer
			Dim db As ReplicationSlaveDatabase = DatabaseFactory.CreateReplicationSlaveDatabase(port)

			db.ReplicationAck = ack
			Dim dbName As String = "replicslave.dbs"
			Tests.TryDeleteFile(dbName)
			Dim dbFile = New OsFile(dbName)
			dbFile.NoFlush = True
			db.Open(dbFile, cacheSizeInBytes)

			Dim total As New DateTime(0)
			Dim n As Integer = 0
			Dim lastKey As Long = 0
			While db.IsConnected()
				db.WaitForModification()
				db.BeginThreadTransaction(TransactionMode.ReplicationSlave)
				Dim root As IFieldIndex(Of Integer, Record) = DirectCast(db.Root, IFieldIndex(Of Integer, Record))
				If root IsNot Nothing AndAlso root.Count = count Then
					Dim start As DateTime = DateTime.Now
					Dim prevKey As Integer = -1
					i = 0
					For Each rec As Record In root
						Dim key As Integer = rec.key
						lastKey = rec.key
						Debug.Assert(prevKey < 0 OrElse key = prevKey + 1)
						prevKey = key
						i += 1
					Next
					Debug.Assert(i = count)
					n += i
					total += (DateTime.Now - start)
				End If
				db.EndThreadTransaction()
				Interlocked.Exchange(slaveCurrKey, lastKey)
				If log AndAlso n Mod 1000 = 0 Then
					Console.WriteLine("Slave processed {0} transactions", n)
				End If
			End While
			db.Close()
			Console.WriteLine("Replication slave finished", n)
		End Sub

		' TODO: use more databases from TestConfig
		Public Sub Run(config As TestConfig)
			config.Result = New TestResult()

			Dim ack As Boolean = False
			Dim async As Boolean = True
			Dim port As Integer = defaultPort
			' start the master thread
			Dim nIterations As Integer = config.Count
			Dim t1 As New Thread(Function() 
			Master(port, async, ack, nIterations)

End Function)
			t1.Name = "ReplicMaster"
			t1.Start()

			Dim t2 As New Thread(Function() 
			Slave(port, async, ack)

End Function)
			t2.Name = "ReplicSlave"
			t2.Start()
			t1.Join()
			t2.Join()
		End Sub
	End Class
End Namespace
#End If
