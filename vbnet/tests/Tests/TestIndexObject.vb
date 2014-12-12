Namespace Volante

	Public Class TestIndexObject
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public str As String
			Public n As Long
			Public Sub New(v As Long)
				n = v
				str = Convert.ToString(v)
			End Sub
			Public Sub New()
			End Sub
		End Class

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim r As Record = Nothing
			Dim count As Integer = config.Count
			Dim res = New TestIndexNumericResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim idx = db.CreateIndex(Of Record, Record)(IndexType.NonUnique)
			db.Root = idx
			Dim val As Long = 1999
			For i = 0 To count - 1
				r = New Record(val)
				r.MakePersistent(db)
				idx.Put(r, r)
				If i Mod 100 = 0 Then
					db.Commit()
				End If
				val = (3141592621L * val + 2718281829L) Mod 1000000007L
			Next

			Tests.Assert(idx.Count = count)
			db.Commit()
			res.InsertTime = DateTime.Now - start
			Tests.Assert(idx.Count = count)
			Dim recs As Record() = idx.ToArray()
			Array.Sort(recs, Function(r1, r2) 
			Return r1.Oid - r2.Oid

End Function)
			Tests.Assert(recs.Length = count)
			Dim min As Record = recs(0)
			Dim max As Record = recs(recs.Length - 1)
			Dim mid As Record = recs(recs.Length \ 2)
			start = System.DateTime.Now
			recs = idx(min, mid)
			i = 0
			For Each r2 As var In recs
				Tests.Assert(r2.Oid >= min.Oid AndAlso r2.Oid <= mid.Oid)
				i += 1
			Next
			recs = idx(mid, max)
			i = 0
			For Each r2 As var In recs
				Tests.Assert(r2.Oid >= mid.Oid AndAlso r2.Oid <= max.Oid)
				i += 1
			Next
			Dim prev As Long = min.Oid
			i = 0
			Dim e1 = idx.GetEnumerator()
			While e1.MoveNext()
				r = e1.Current
				Tests.Assert(r.Oid >= prev)
				prev = r.Oid
				i += 1
			End While
			Tests.VerifyEnumeratorDone(e1)

			prev = min.Oid
			i = 0
			For Each r2 As var In idx
				Tests.Assert(r2.Oid >= prev)
				prev = r2.Oid
				i += 1
			Next

			prev = min.Oid
			i = 0
			For Each r2 As var In idx.Range(min, max, IterationOrder.AscentOrder)
				Tests.Assert(r2.Oid >= prev)
				prev = r2.Oid
				i += 1
			Next

			prev = max.Oid
			i = 0
			For Each r2 As var In idx.Range(min, max, IterationOrder.DescentOrder)
				Tests.Assert(prev >= r2.Oid)
				prev = r2.Oid
				i += 1
			Next

			prev = max.Oid
			i = 0
			For Each r2 As var In idx.Reverse()
				Tests.Assert(prev >= r2.Oid)
				prev = r2.Oid
				i += 1
			Next
			Dim usedBeforeDelete As Long = db.UsedSize
			recs = idx(min, max)
			i = 0
			For Each r2 As var In recs
				Tests.Assert(Not r2.IsDeleted())
				idx.Remove(r2, r2)
				r2.Deallocate()
				i += 1
			Next
			Tests.Assert(idx.Count = 0)
			db.Commit()
			Dim usedAfterDelete As Long = db.UsedSize
			db.Gc()
			db.Commit()
			Dim usedAfterGc As Long = db.UsedSize
			db.Close()
		End Sub
	End Class
End Namespace
