Namespace Volante

	Public Class TestIndexGuid
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public nval As Guid
			' native value
			Public Sub New(v As Guid)
				nval = v
			End Sub
			Public Sub New()
			End Sub
		End Class

		Shared ReadOnly min As New Guid(New Byte() {0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0})
		Shared ReadOnly max As New Guid(New Byte() {&Hff, &Hff, &Hff, &Hff, &Hff, &Hff, _
			&Hff, &Hff, &Hff, &Hff, &Hff, &Hff, _
			&Hff, &Hff, &Hff, &Hff})
		Shared ReadOnly mid As New Guid(New Byte() {&H7f, &Hff, &Hff, &Hff, &Hff, &Hff, _
			&Hff, &Hff, &Hff, &Hff, &Hff, &Hff, _
			&Hff, &Hff, &Hff, &Hff})

		Public Shared Sub pack4(arr As Byte(), offs As Integer, val As Integer)
			arr(offs) = CByte(val >> 24)
			arr(offs + 1) = CByte(val >> 16)
			arr(offs + 2) = CByte(val >> 8)
			arr(offs + 3) = CByte(val)
		End Sub
		Public Shared Sub pack8(arr As Byte(), offs As Integer, val As Long)
			pack4(arr, offs, CInt(val >> 32))
			pack4(arr, offs + 4, CInt(val))
		End Sub

		Private Shared Function Clamp(n As Long) As Guid
			Dim bytes = New Byte(15) {}
			pack8(bytes, 0, n)
			Return New Guid(bytes)
		End Function

		Public Sub Run(config As TestConfig)
			Dim i As Integer, cmp As Integer
			Dim r As Record = Nothing
			Dim count As Integer = config.Count
			Dim res = New TestIndexNumericResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim idx = db.CreateIndex(Of Guid, Record)(IndexType.NonUnique)
			db.Root = idx
			Dim val As Long = 1999
			For i = 0 To count - 1
				Dim idxVal As Guid = Clamp(val)
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
			res.InsertTime = DateTime.Now - start
			Tests.Assert(idx.Count = count + 2)

			start = System.DateTime.Now
			Dim recs As Record() = idx(min, mid)
			i = 0
			For Each r2 As var In recs
				cmp = min.CompareTo(r2.nval)
				Tests.Assert(cmp = -1 OrElse cmp = 0)
				cmp = mid.CompareTo(r2.nval)
				Tests.Assert(cmp = 1 OrElse cmp = 0)
				i += 1
			Next
			recs = idx(mid, max)
			i = 0
			For Each r2 As var In recs
				cmp = mid.CompareTo(r2.nval)
				Tests.Assert(cmp = -1 OrElse cmp = 0)
				cmp = max.CompareTo(r2.nval)
				Tests.Assert(cmp = 1 OrElse cmp = 0)
				i += 1
			Next
			Dim prev As Guid = min
			i = 0
			Dim e1 = idx.GetEnumerator()
			While e1.MoveNext()
				r = e1.Current
				cmp = r.nval.CompareTo(prev)
				Tests.Assert(cmp = 1 OrElse cmp = 0)
				prev = r.nval
				i += 1
			End While
			Tests.VerifyEnumeratorDone(e1)

			prev = min
			i = 0
			For Each r2 As var In idx
				cmp = r2.nval.CompareTo(prev)
				Tests.Assert(cmp = 1 OrElse cmp = 0)
				prev = r2.nval
				i += 1
			Next

			prev = min
			i = 0
			For Each r2 As var In idx.Range(min, max, IterationOrder.AscentOrder)
				cmp = r2.nval.CompareTo(prev)
				Tests.Assert(cmp = 1 OrElse cmp = 0)
				prev = r2.nval
				i += 1
			Next

			prev = max
			i = 0
			For Each r2 As var In idx.Range(min, max, IterationOrder.DescentOrder)
				cmp = r2.nval.CompareTo(prev)
				Tests.Assert(cmp = -1 OrElse cmp = 0)
				prev = r2.nval
				i += 1
			Next

			prev = max
			i = 0
			For Each r2 As var In idx.Reverse()
				cmp = r2.nval.CompareTo(prev)
				Tests.Assert(cmp = -1 OrElse cmp = 0)
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
