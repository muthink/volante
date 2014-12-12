' Copyright: Krzysztof Kowalczyk
' License: BSD
' Smaller test cases that don't deserve their own file

Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Namespace Volante

	Public Class TestRemove00
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public dt As DateTime
			Public lval As Long

			' persistent objects require empty constructor
			Public Sub New()
			End Sub

			Public Sub New(val As Long)
				Me.lval = val
				Me.dt = DateTime.Now
			End Sub
		End Class

		Public Class Root
			Inherits Persistent
			Public idx As IIndex(Of Long, Record)
		End Class

		' Test that IIndex.Remove(K key) throws on non-unique index.
		' Test that clearing the index removes all objects pointed by
		' the index
		Public Sub Run(config As TestConfig)
			Dim n As Integer
			Dim db As IDatabase = config.GetDatabase()
			Dim root As New Root()
			root.idx = db.CreateIndex(Of Long, Record)(IndexType.NonUnique)
			db.Root = root

			root.idx.Put(1, New Record(1))
			root.idx.Put(2, New Record(1))
			root.idx.Put(3, New Record(2))
			db.Commit()
			n = Tests.DbInstanceCount(db, GetType(Record))
			Tests.Assert(3 = n)
			Tests.AssertDatabaseException(Function() 
			root.idx.Remove(New Key(CLng(1)))

End Function, DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			root.idx = Nothing
			root.Modify()
			db.Commit()
			n = Tests.DbInstanceCount(db, GetType(Record))
			Tests.Assert(0 = n)
			db.Close()
		End Sub
	End Class

	' test that deleting an object referenced by another objects
	' corrupts the database.
	Public Class TestCorrupt00
		Implements ITest
		Public Class Root
			Inherits Persistent
			Public r As RecordFull
		End Class

		Public Sub Run(config As TestConfig)
			Dim db As IDatabase = config.GetDatabase()
			Dim root As New Root()
			Dim r = New RecordFull()
			root.r = r
			db.Root = root
			db.Commit()
			' delete the object from the database
			r.Deallocate()
			db.Commit()
			db.Close()

			db = config.GetDatabase(False)
			' r was explicitly deleted from the database but it's
			' still referenced by db.Root. Loading root object will
			' try to recursively load Record object but since it's
			' been deleted, we should get an exception
			Tests.AssertDatabaseException(Function() 
			root = DirectCast(db.Root, Root)

End Function, DatabaseException.ErrorCode.DELETED_OBJECT)
			r = root.r
			db.Close()
		End Sub
	End Class

	' force recover by not closing the database propery
	Public Class TestCorrupt01
		Implements ITest
		Public Class Root
			Inherits Persistent
			Public idx As IIndex(Of String, RecordFull)
		End Class

		Public Sub Run(config As TestConfig)
			Debug.Assert(Not config.IsTransient)

			Dim db As IDatabase = DatabaseFactory.CreateDatabase()
			Tests.AssertDatabaseException(Function() 
			Dim r = db.Root

End Function, DatabaseException.ErrorCode.DATABASE_NOT_OPENED)

			db = config.GetDatabase()
			Dim root As New Root()
			Dim idx = db.CreateIndex(Of String, RecordFull)(IndexType.NonUnique)
			root.idx = idx
			db.Root = root
			db.Commit()

			For i As Integer = 0 To 9
				Dim r = New RecordFull(i)
				idx.Put(r.StrVal, r)
			Next
			Dim f = db.File
			Dim [of] As OsFile = DirectCast(f, OsFile)
			[of].Close()

			Dim db2 As IDatabase = config.GetDatabase(False)
			Try
				db.Close()
			Catch
			End Try
			db2.Close()
		End Sub
	End Class

	' Corner cases for key search
	Public Class TestIndexRangeSearch
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public lval As Long
			Public data As Byte()

			Public Sub New()
				data = New Byte(3) {1, 4, 0, 3}
			End Sub
		End Class

		Public Class Root
			Inherits Persistent
			Public idx As IIndex(Of Long, Record)
		End Class

		Public Sub Run(config As TestConfig)
			Dim recs As Record()
			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.IsOpened)
			Tests.AssertDatabaseException(Function() 
			db.Open(New NullFile(), 0)

End Function, DatabaseException.ErrorCode.DATABASE_ALREADY_OPENED)

			Dim expectedData = New Byte(3) {1, 4, 0, 3}

			Dim root As New Root()
			root.idx = db.CreateIndex(Of Long, Record)(IndexType.Unique)
			db.Root = root
			root.idx(1) = New Record() With { _
				Key .lval = 1 _
			}
			root.idx(2) = New Record() With { _
				Key .lval = 2 _
			}
			root.idx(4) = New Record() With { _
				Key .lval = 4 _
			}
			root.idx(5) = New Record() With { _
				Key .lval = 5 _
			}
			db.Commit()
			Tests.Assert(db.DatabaseSize > 0)
			recs = root.idx(-1, -1)
			Tests.Assert(recs.Length = 0)
			recs = root.idx(0, 0)
			Tests.Assert(recs.Length = 0)
			recs = root.idx(1, 1)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			recs = root.idx(2, 2)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			recs = root.idx(3, 3)
			Tests.Assert(recs.Length = 0)
			recs = root.idx(5, 5)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			recs = root.idx(6, 6)
			Tests.Assert(recs.Length = 0)
			recs = root.idx(Long.MinValue, Long.MaxValue)
			Tests.Assert(recs.Length = 4)
			recs = root.idx(1, 5)
			Tests.Assert(recs.Length = 4)
			recs = root.idx(Long.MinValue, Long.MinValue)
			Tests.Assert(recs.Length = 0)
			recs = root.idx(Long.MaxValue, Long.MaxValue)
			Tests.Assert(recs.Length = 0)

			recs = GetInRange(root.idx, -1)
			Tests.Assert(recs.Length = 0)
			recs = GetInRange(root.idx, 0)
			Tests.Assert(recs.Length = 0)
			recs = GetInRange(root.idx, 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			Tests.Assert(recs.Length = 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			recs = GetInRange(root.idx, 2)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			recs = GetInRange(root.idx, 3)
			Tests.Assert(recs.Length = 0)
			recs = GetInRange(root.idx, 5)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(Tests.ByteArraysEqual(recs(0).data, expectedData))
			recs = GetInRange(root.idx, 6)
			Tests.Assert(recs.Length = 0)

			db.Close()
			Tests.Assert(Not db.IsOpened)
		End Sub

		Private Function GetInRange(idx As IIndex(Of Long, Record), range As Long) As Record()
			Dim recs As New List(Of Record)()
			For Each r As var In idx.Range(range, range)
				recs.Add(r)
			Next
			Return recs.ToArray()
		End Function
	End Class
End Namespace
