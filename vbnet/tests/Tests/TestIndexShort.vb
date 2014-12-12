Namespace Volante

	Public Class TestIndexShort
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public lval As Long
			Public nval As Short
			' native value
			Public Sub New(v As Short)
				nval = v
				lval = CLng(v)
			End Sub
			Public Sub New()
			End Sub
		End Class

		Const min As Short = Short.MinValue
		Const max As Short = Short.MaxValue
		Const mid As Short = 0

		Private Shared Function Clamp(n As Long) As Short
			Dim range As Long = max - min
			Dim val As Long = (n Mod range) + CLng(min)
			Return CShort(val)
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
			Dim idx = db.CreateIndex(Of Short, Record)(IndexType.NonUnique)
			db.Root = idx
			Dim countOf1999 As Integer = 0
			i = 0
			For Each val As Long In Tests.KeySeq(count)
				Dim idxVal As Short = Clamp(val)
				If idxVal = 1999 Then
					countOf1999 += 1
				End If
				Tests.Assert(idxVal <> max)
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
			Tests.Assert(1 = recs.Length)

			recs = idx(max, max)
			Tests.Assert(1 = recs.Length)

			recs = idx(1999, 1999)
			Tests.Assert(countOf1999 = recs.Length)

			recs = idx(min + 1, min + 1)
			Tests.Assert(0 = recs.Length)

			Dim prev As Short = min
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
