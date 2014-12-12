' Copyright: Krzysztof Kowalczyk
' License: BSD

Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestLinkPArray
		Implements ITest
		Public Class RelMember
			Inherits Persistent
			Private l As Long

			Public Sub New()
			End Sub

			Public Sub New(v As Long)
				l = v
			End Sub
		End Class

		Public Class Root
			Inherits Persistent
			Public arr As IPArray(Of RecordFull)
			Public link As ILink(Of RecordFull)
			Public relOwner As RecordFull
			Public rel As Relation(Of RelMember, RecordFull)
		End Class

		Public Sub Run(config As TestConfig)
			Dim r As RecordFull
			Dim rm As RelMember
			Dim recs As RecordFull()
			Dim rmArr As RelMember()
			Dim notInArr1 As RecordFull
			Dim db As IDatabase = config.GetDatabase()
			config.Result = New TestResult()
			Dim root As New Root()
			Dim arr = db.CreateArray(Of RecordFull)(256)
			arr = db.CreateArray(Of RecordFull)()
			Dim link = db.CreateLink(Of RecordFull)(256)
			link = db.CreateLink(Of RecordFull)()
			root.relOwner = New RecordFull()
			Dim rel = db.CreateRelation(Of RelMember, RecordFull)(root.relOwner)
			Tests.Assert(rel.Owner = root.relOwner)
			rel.SetOwner(New RecordFull(88))
			Tests.Assert(rel.Owner <> root.relOwner)
			rel.Owner = root.relOwner
			Tests.Assert(rel.Owner = root.relOwner)
			root.arr = arr
			root.link = link
			root.rel = rel
			db.Root = root
			Tests.Assert(arr.Count = 0)
			Tests.Assert(DirectCast(arr, IGenericPArray).Size() = 0)

			Dim inMem = New List(Of RecordFull)()
			For i As Long = 0 To 255
				r = New RecordFull(i)
				rm = New RelMember(i)
				inMem.Add(r)
				arr.Add(r)
				Tests.Assert(arr.Count = i + 1)
				link.Add(r)
				rel.Add(rm)
				Tests.Assert(link.Count = i + 1)
			Next
			recs = arr.ToArray()
			rmArr = rel.ToArray()
			Tests.Assert(recs.Length = rmArr.Length)
			Tests.Assert(rel.Count = rel.Length)
			Tests.Assert(rel.Size() = rel.Count)
			rel.CopyTo(rmArr, 0)
			Tests.Assert(recs.Length = arr.Length)
			For j As Integer = 0 To recs.Length - 1
				Tests.Assert(recs(j) = arr(j))
				Tests.Assert(rmArr(j) = rel(j))
			Next
			recs = inMem.ToArray()

			arr.AddAll(recs)

			rel.AddAll(rmArr)

			notInArr1 = New RecordFull(256)
			inMem.Add(notInArr1)
			db.Commit()

			Dim e = link.GetEnumerator()
			Dim idx As Integer = 0
			While e.MoveNext()
				Tests.Assert(e.Current = inMem(System.Math.Max(System.Threading.Interlocked.Increment(idx),idx - 1)))
			End While
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim tmp = e.Current

End Function)
			Tests.Assert(Not e.MoveNext())
			e.Reset()
			idx = 0
			Dim nullCount As Integer = 0
			While e.MoveNext()
				Tests.Assert(e.Current = inMem(System.Math.Max(System.Threading.Interlocked.Increment(idx),idx - 1)))
				Dim e2 As IEnumerator = DirectCast(e, IEnumerator)
				If e2.Current Is Nothing Then
					nullCount += 1
				End If
			End While

			Dim e3 = rel.GetEnumerator()
			While e3.MoveNext()
				Tests.Assert(e3.Current IsNot Nothing)
			End While
			Tests.Assert(Not e3.MoveNext())
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim tmp = e3.Current

End Function)
			e3.Reset()
			Tests.Assert(e3.MoveNext())

			nullCount = 0
			For Each r2 As var In link
				If r2 Is Nothing Then
					nullCount += 1
				End If
			Next

			Tests.Assert(arr.Length = 512)
			Dim a As Array = arr.ToArray()
			Tests.Assert(a.Length = 512)
			a = arr.ToRawArray()
			Tests.Assert(a.Length = 512)

			arr.RemoveAt(0)
			db.Commit()

			Tests.Assert(arr.Count = 511)
			arr.RemoveAt(arr.Count - 1)
			db.Commit()
			Tests.Assert(arr.Count = 510)
			r = arr(5)
			Tests.Assert(arr.Contains(r))
			Tests.Assert(Not arr.Contains(Nothing))
			Tests.Assert(Not arr.Contains(notInArr1))
			Tests.Assert(arr.ContainsElement(5, r))
			Tests.Assert(Not arr.IsReadOnly)
			Tests.Assert(Not link.IsReadOnly)
			Tests.Assert(5 = arr.IndexOf(r))
			Tests.Assert(-1 = arr.IndexOf(notInArr1))
			Tests.Assert(-1 = arr.IndexOf(Nothing))
			Tests.Assert(r.Oid = arr.GetOid(5))
			arr(5) = New RecordFull(17)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			r = arr(1024)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.Insert(9999, Nothing)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			link.Insert(9999, Nothing)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.Insert(-1, Nothing)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			link.Insert(-1, Nothing)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.RemoveAt(9999)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.RemoveAt(-1)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.GetOid(9999)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.GetOid(-1)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.GetRaw(9999)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.GetRaw(-1)

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.[Set](9999, New RecordFull(9988))

End Function)
			Tests.AssertException(Of IndexOutOfRangeException)(Function() 
			arr.[Set](-1, New RecordFull(9988))

End Function)

			Tests.Assert(arr.GetRaw(8) IsNot Nothing)

			arr.[Set](25, arr(24))
			arr.Pin()
			arr.Unpin()
			Tests.Assert(arr.Remove(arr(12)))
			Tests.Assert(Not arr.Remove(notInArr1))
			Tests.Assert(link.Remove(link(3)))
			Tests.Assert(Not link.Remove(notInArr1))
			Tests.Assert(arr.Length = 509)
			arr.Insert(5, New RecordFull(88))
			Tests.Assert(arr.Length = 510)
			link.Insert(5, New RecordFull(88))
			Dim expectedCount As Integer = arr.Count + link.Count
			arr.AddAll(link)
			Tests.Assert(arr.Count = expectedCount)

			Tests.Assert(arr.GetEnumerator() IsNot Nothing)
			Tests.Assert(link.GetEnumerator() IsNot Nothing)

			link.Length = 1024
			Tests.Assert(link.Length = 1024)
			link.Length = 128
			Tests.Assert(link.Length = 128)
			link.AddAll(arr)
			arr.Clear()
			Tests.Assert(0 = arr.Length)
			db.Commit()
			arr.AddAll(link)
			arr.AddAll(arr)
			recs = arr.ToArray()
			link.AddAll(New RecordFull(0) {recs(0)})
			link.AddAll(recs, 1, 1)
			db.Commit()
			recs = link.ToArray()
			Tests.Assert(recs.Length = link.Length)
			link.Length = link.Length - 2

			rel.Length = rel.Length / 2
			idx = rel.Length / 2
			Tests.Assert(rel.[Get](idx) IsNot Nothing)
			rel(idx) = New RelMember(55)
			db.Commit()
			Dim raw As IPersistent = rel.GetRaw(idx)
			Tests.Assert(raw.IsRaw())
			rm = rel(idx)
			Tests.Assert(rel.Contains(rm))
			Tests.Assert(rel.ContainsElement(idx, rm))
			Tests.Assert(rel.Remove(rm))
			Tests.Assert(Not rel.Contains(rm))
			Tests.Assert(Not rel.Remove(rm))
			idx = rel.Length / 2
			rm = rel(idx)
			Tests.Assert(idx = rel.IndexOf(rm))
			Dim cnt As Integer = rel.Count
			rel.RemoveAt(idx)
			Tests.Assert(rel.Count = cnt - 1)
			Tests.Assert(Not rel.Contains(rm))
			rel.Add(rm)
			db.Commit()
			'TODO: LinkImpl.ToRawArray() seems wrong but changing it
			'breaks a lot of code
			'Array ra = rel.ToRawArray();
			Dim ra2 As Array = rel.ToArray()
			'Tests.Assert(ra2.Length == ra.Length);
			'Tests.Assert(ra.Length == rel.Count);
			rel.Insert(1, New RelMember(123))
			'Tests.Assert(rel.Count == ra.Length + 1);
			rel.Unpin()
			rel.Pin()
			rel.Unpin()
			rel.Clear()
			Tests.Assert(rel.Count = 0)
			db.Close()
		End Sub
	End Class
End Namespace
