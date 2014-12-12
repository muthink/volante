Namespace Volante

	Public Class TestIndexDateTime
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public nval As DateTime
			' native value
			Public Sub New(v As DateTime)
				nval = v
			End Sub
			Public Sub New()
			End Sub
		End Class

		Shared ReadOnly min As DateTime = DateTime.MinValue
		Shared ReadOnly max As DateTime = DateTime.MaxValue
		Shared ReadOnly mid As New DateTime(max.Ticks \ 2)

		Private Shared Function Clamp(n As Long) As DateTime
			Return New DateTime(n)
		End Function

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim r As Record = Nothing
			Dim count As Integer = config.Count
			Dim res = New TestIndexNumericResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim idx = db.CreateIndex(Of DateTime, Record)(IndexType.NonUnique)
			db.Root = idx
			Dim val As Long = 1999
			For i = 0 To count - 1
				Dim idxVal As DateTime = Clamp(val)
				r = New Record(idxVal)
				idx.Put(idxVal, r)
				If i Mod 100 = 0 Then
					db.Commit()
				End If
				val = (3141592621L * val + 2718281829L) Mod 1000000007L
			Next
			idx.Put(min, New Record(min))
			idx.Put(max, New Record(max))

			Tests.Assert(idx.Count = count + 2)
			db.Commit()
			db.Close()
			res.InsertTime = DateTime.Now - start
			Tests.Assert(idx.Count = count + 2)

			start = System.DateTime.Now
			db = config.GetDatabase(False)
			idx = DirectCast(db.Root, IIndex(Of DateTime, Record))
			Dim recs As Record() = idx(min, mid)
			i = 0
			For Each r2 As var In recs
				Tests.Assert(r2.nval >= min AndAlso r2.nval <= mid)
				i += 1
			Next
			recs = idx(mid, max)
			i = 0
			For Each r2 As var In recs
				Tests.Assert(r2.nval >= mid AndAlso r2.nval <= max)
				i += 1
			Next
			Dim prev As DateTime = min
			i = 0
			Dim e1 = idx.GetEnumerator()
			While e1.MoveNext()
				r = e1.Current
				Tests.Assert(r.nval >= prev)
				prev = r.nval
				i += 1
			End While
			Tests.VerifyEnumeratorDone(e1)

			prev = min
			i = 0
			For Each r2 As var In idx
				Tests.Assert(r2.nval >= prev)
				prev = r2.nval
				i += 1
			Next

			prev = min
			i = 0
			For Each r2 As var In idx.Range(min, max, IterationOrder.AscentOrder)
				Tests.Assert(r2.nval >= prev)
				prev = r2.nval
				i += 1
			Next

			prev = max
			i = 0
			For Each r2 As var In idx.Range(min, max, IterationOrder.DescentOrder)
				Tests.Assert(prev >= r2.nval)
				prev = r2.nval
				i += 1
			Next

			prev = max
			i = 0
			For Each r2 As var In idx.Reverse()
				Tests.Assert(prev >= r2.nval)
				prev = r2.nval
				i += 1
			Next
			Dim usedBeforeDelete As Long = db.UsedSize
			recs = idx(min, max)
			i = 0
			For Each r2 As var In recs
				Tests.Assert(Not r2.IsDeleted())
				idx.Remove(r2.nval, r2)
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
