Namespace Volante

	Public Class TestIndexBooleanResult
		Inherits TestResult
		Public InsertTime As TimeSpan
	End Class

	Public Class TestIndexBoolean
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public val As Long
			Public Function ToBool() As Boolean
				Return val Mod 2 = 1
			End Function
		End Class

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim r As Record = Nothing
			Dim count As Integer = config.Count
			Dim res = New TestIndexBooleanResult()
			config.Result = res
			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim idx = db.CreateIndex(Of [Boolean], Record)(IndexType.NonUnique)
			db.Root = idx

			Dim val As Long = 1999
			Dim falseCount As Integer = 0
			Dim trueCount As Integer = 0
			For i = 0 To count - 1
				r = New Record()
				r.val = val
				idx.Put(r.ToBool(), r)
				If r.ToBool() Then
					trueCount += 1
				Else
					falseCount += 1
				End If
				If i Mod 1000 = 0 Then
					db.Commit()
				End If
				val = (3141592621L * val + 2718281829L) Mod 1000000007L
			Next
			Tests.Assert(count = trueCount + falseCount)
			db.Commit()
			Tests.Assert(idx.Count = count)
			res.InsertTime = DateTime.Now - start

			start = System.DateTime.Now
			Tests.AssertDatabaseException(Function() 
			r = idx(True)

End Function, DatabaseException.ErrorCode.KEY_NOT_UNIQUE)

			Tests.AssertDatabaseException(Function() 
			r = idx(False)

End Function, DatabaseException.ErrorCode.KEY_NOT_UNIQUE)

			Dim recs As Record() = idx(True, True)
			Tests.Assert(recs.Length = trueCount)
			For Each r2 As var In recs
				Tests.Assert(r2.ToBool())
			Next
			recs = idx(False, False)
			Tests.Assert(recs.Length = falseCount)
			For Each r2 As var In recs
				Tests.Assert(Not r2.ToBool())
			Next

			Dim e1 = idx.GetEnumerator(False, True, IterationOrder.AscentOrder)
			Dim first As Record = Nothing
			i = 0
			While e1.MoveNext()
				r = e1.Current
				If first Is Nothing Then
					first = r
				End If
				If i < falseCount Then
					Tests.Assert(Not r.ToBool())
				Else
					Tests.Assert(r.ToBool())
				End If
				i += 1
			End While
			Tests.VerifyEnumeratorDone(e1)

			e1 = idx.GetEnumerator(False, True, IterationOrder.DescentOrder)
			i = 0
			While e1.MoveNext()
				r = e1.Current
				If i < trueCount Then
					Tests.Assert(r.ToBool())
				Else
					Tests.Assert(Not r.ToBool())
				End If
				i += 1
			End While
			Tests.Assert(first.val = r.val)
			Tests.VerifyEnumeratorDone(e1)

			i = 0
			For Each r2 As var In idx.Range(False, True)
				If i < falseCount Then
					Tests.Assert(Not r2.ToBool())
				Else
					Tests.Assert(r2.ToBool())
				End If
				i += 1
			Next
			Tests.Assert(i = count)

			i = 0
			For Each r2 As var In idx.Range(False, True, IterationOrder.DescentOrder)
				If i < trueCount Then
					Tests.Assert(r2.ToBool())
				Else
					Tests.Assert(Not r2.ToBool())
				End If
				i += 1
			Next
			Tests.Assert(i = count)

			i = 0
			For Each r2 As var In idx.Reverse()
				If i < trueCount Then
					Tests.Assert(r2.ToBool())
				Else
					Tests.Assert(Not r2.ToBool())
				End If
				i += 1
			Next
			Tests.Assert(i = count)

			Tests.Assert(idx.KeyType = GetType([Boolean]))
			Tests.AssertDatabaseException(Function() idx.Remove(New Key(True)), DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			Tests.AssertDatabaseException(Function() idx.RemoveKey(True), DatabaseException.ErrorCode.KEY_NOT_UNIQUE)

			recs = idx(False, True)
			Tests.Assert(recs.Length = idx.Count)

			i = 0
			Dim removedTrue As Integer = 0
			Dim removedFalse As Integer = 0
			For Each r2 As var In recs
				Dim b = r2.ToBool()
				If i Mod 3 = 1 Then
					idx.Remove(b, r2)
					If r2.ToBool() Then
						removedTrue += 1
					Else
						removedFalse += 1
					End If
					r2.Deallocate()
				End If
				i += 1
			Next
			db.Commit()

			count -= (removedTrue + removedFalse)
			falseCount -= removedFalse
			trueCount -= removedTrue
			Tests.Assert(idx.Count = count)
			db.Close()
		End Sub

	End Class
End Namespace
