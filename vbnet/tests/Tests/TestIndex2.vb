Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestIndex2Result
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public IndexSearch As TimeSpan
		Public IterationTime As TimeSpan
		Public RemoveTime As TimeSpan
		Public MemoryUsage As ICollection(Of TypeMemoryUsage)
		' elements are of TypeMemoryUsage type
	End Class

	Public Class TestIndex2
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public strKey As String
			Public intKey As Long
		End Class

		Public Class Root
			Inherits Persistent
			Public strIndex As ISortedCollection(Of String, Record)
			Public intIndex As ISortedCollection(Of Long, Record)
		End Class

		Public Class IntRecordComparator
			Inherits PersistentComparator(Of Long, Record)
			Public Overrides Function CompareMembers(m1 As Record, m2 As Record) As Integer
				Dim diff As Long = m1.intKey - m2.intKey
				Return If(diff < 0, -1, If(diff = 0, 0, 1))
			End Function

			Public Overrides Function CompareMemberWithKey(mbr As Record, key As Long) As Integer
				Dim diff As Long = mbr.intKey - key
				Return If(diff < 0, -1, If(diff = 0, 0, 1))
			End Function
		End Class

		Public Class StrRecordComparator
			Inherits PersistentComparator(Of String, Record)
			Public Overrides Function CompareMembers(m1 As Record, m2 As Record) As Integer
				Return m1.strKey.CompareTo(m2.strKey)
			End Function

			Public Overrides Function CompareMemberWithKey(mbr As Record, key As String) As Integer
				Return mbr.strKey.CompareTo(key)
			End Function
		End Class

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim count As Integer = config.Count
			Dim res = New TestIndex2Result()
			config.Result = res
			Dim start = DateTime.Now

			Dim db As IDatabase = config.GetDatabase()
			Dim root As Root = DirectCast(db.Root, Root)
			Tests.Assert(root Is Nothing)
			root = New Root()
			root.strIndex = db.CreateSortedCollection(Of String, Record)(New StrRecordComparator(), IndexType.Unique)
			root.intIndex = db.CreateSortedCollection(Of Long, Record)(New IntRecordComparator(), IndexType.Unique)
			db.Root = root

			Dim intIndex As ISortedCollection(Of Long, Record) = root.intIndex
			Dim strIndex As ISortedCollection(Of String, Record) = root.strIndex

			For Each key As var In Tests.KeySeq(count)
				Dim rec As New Record()
				rec.intKey = key
				rec.strKey = System.Convert.ToString(key)
				intIndex.Add(rec)
				strIndex.Add(rec)
			Next
			db.Commit()
			res.InsertTime = DateTime.Now - start

			start = System.DateTime.Now

			For Each key As var In Tests.KeySeq(count)
				Dim rec1 As Record = intIndex(key)
				Dim rec2 As Record = strIndex(Convert.ToString(key))
			Next
			res.IndexSearch = DateTime.Now - start

			start = System.DateTime.Now
			Dim k = Int64.MinValue
			i = 0
			For Each rec As Record In intIndex
				Tests.Assert(rec.intKey >= k)
				k = rec.intKey
				i += 1
			Next
			Tests.Assert(i = count)
			i = 0
			Dim strKey As [String] = ""
			For Each rec As Record In strIndex
				Tests.Assert(rec.strKey.CompareTo(strKey) >= 0)
				strKey = rec.strKey
				i += 1
			Next
			Tests.Assert(i = count)
			res.IterationTime = DateTime.Now - start

			start = DateTime.Now
			res.MemoryUsage = db.GetMemoryUsage().Values

			start = System.DateTime.Now
			For Each key As var In Tests.KeySeq(count)
				Dim rec As Record = intIndex(key)
				intIndex.Remove(rec)
				strIndex.Remove(rec)
				rec.Deallocate()
			Next
			res.RemoveTime = DateTime.Now - start
			db.Close()
			'Tests.DumpMemoryUsage(res.TypeMemoryUsage);
		End Sub
	End Class
End Namespace
