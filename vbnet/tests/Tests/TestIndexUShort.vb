Namespace Volante

	Public Class TestIndexUShort
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public lval As Long
			Public nval As UShort
			' native value
			Public Sub New(v As UShort)
				nval = v
				lval = CLng(v)
			End Sub
			Public Sub New()
			End Sub
		End Class

		Const min As UShort = UShort.MinValue
		Const max As UShort = UShort.MaxValue
		Const mid As UShort = max \ 2

		Private Shared Function Clamp(n As Long) As UShort
			Dim val As Long = n Mod max
			Return CUShort(val)
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
			Dim idx = db.CreateIndex(Of UShort, Record)(IndexType.NonUnique)
			db.Root = idx
			Dim countOf1999 As Integer = 0
			i = 0
			For Each val As var In Tests.KeySeq(count)
				Dim idxVal As UShort = Clamp(val)
				If val = 1999 Then
					countOf1999 += 1
				End If
				r = New Record(idxVal)
				idx.Put(idxVal, r)
				i += 1
				If i Mod 100 = 0 Then
					db.Commit()
				End If
			Next
			idx.Put(min, New Record(min))
			idx.Put(max, New Record(max))

			Tests.Assert(idx.Count = count + 2)
			db.Commit()
			res.InsertTime = DateTime.Now - start
			Tests.Assert(idx.Count = count + 2)

			start = System.DateTime.Now
			Dim recs As Record() = idx(min, mid)
			For Each r2 As var In recs
				Tests.Assert(r2.lval >= min AndAlso r2.lval <= mid)
			Next
			recs = idx(mid, max)
			For Each r2 As var In recs
				Tests.Assert(r2.lval >= mid AndAlso r2.lval <= max)
			Next

			recs = idx(min, max)
			Tests.Assert(recs.Length = count + 2)

			recs = idx(min, min)
			Tests.Assert(recs.Length >= 1)

			recs = idx(max, max)
			Tests.Assert(1 = recs.Length)

			' TODO: figure out why returns no values
			recs = idx(1999, 1999)
			Tests.Assert(1 = recs.Length)

			Dim prev As UShort = min
			Dim e1 = idx.GetEnumerator()
			While e1.MoveNext()
				r = e1.Current
				Tests.Assert(r.nval >= prev)
				prev = r.nval
			End While
			Tests.VerifyEnumeratorDone(e1)

			prev = min
			For Each r2 As var In idx
				Tests.Assert(r2.nval >= prev)
				prev = r2.nval
			Next

			prev = min
			For Each r2 As var In idx.Range(min, max, IterationOrder.AscentOrder)
				Tests.Assert(r2.nval >= prev)
				prev = r2.nval
			Next

			prev = max
			For Each r2 As var In idx.Range(min, max, IterationOrder.DescentOrder)
				Tests.Assert(prev >= r2.nval)
				prev = r2.nval
			Next

			prev = max
			For Each r2 As var In idx.Reverse()
				Tests.Assert(prev >= r2.nval)
				prev = r2.nval
			Next
			Dim usedBeforeDelete As Long = db.UsedSize
			recs = idx(min, max)
			For Each r2 As var In recs
				Tests.Assert(Not r2.IsDeleted())
				idx.Remove(r2.nval, r2)
				r2.Deallocate()
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
