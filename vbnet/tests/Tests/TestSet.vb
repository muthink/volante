Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestSet
		Implements ITest
		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim count As Integer = config.Count
			Dim res = New TestIndexNumericResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim [set] = db.CreateSet(Of RecordFull)()
			db.Root = [set]
			Dim val As Long = 1999
			Dim recs = New List(Of RecordFull)()
			Dim rand = New Random()
			For i = 0 To count - 1
				Dim r = New RecordFull(val)
				Tests.Assert(Not [set].Contains(r))
				[set].Add(r)
				[set].Add(r)
				If recs.Count < 10 AndAlso rand.[Next](0, 20) = 4 Then
					recs.Add(r)
				End If

				Tests.Assert([set].Contains(r))
				If i Mod 100 = 0 Then
					db.Commit()
				End If
				val = (3141592621L * val + 2718281829L) Mod 1000000007L
			Next

			Tests.Assert([set].Count = count)
			db.Commit()
			Tests.Assert([set].Count = count)
			Tests.Assert([set].IsReadOnly = False)
			Tests.Assert([set].ContainsAll(recs))

			Dim rOne = New RecordFull(val)
			Tests.Assert(Not [set].Contains(rOne))
			Tests.Assert(Not [set].ContainsAll(New RecordFull() {rOne}))
			Tests.Assert([set].AddAll(New RecordFull() {rOne}))
			Tests.Assert(Not [set].AddAll(recs))
			Tests.Assert([set].Count = count + 1)
			Tests.Assert([set].Remove(rOne))
			Tests.Assert(Not [set].Remove(rOne))

			Tests.Assert([set].RemoveAll(recs))
			Tests.Assert(Not [set].RemoveAll(recs))
			Tests.Assert([set].Count = count - recs.Count)
			Tests.Assert([set].AddAll(recs))
			Tests.Assert([set].Count = count)
			db.Commit()

			res.InsertTime = DateTime.Now - start

			Dim e As IEnumerator = [set].GetEnumerator()
			Tests.Assert(e IsNot Nothing)

			start = System.DateTime.Now
			Tests.Assert(Not [set].Equals(Nothing))
			Tests.Assert([set].Equals([set]))

			Dim set2 = db.CreateSet(Of RecordFull)()
			Tests.Assert(Not [set].Equals(set2))
			For Each r2 As var In [set]
				Tests.Assert([set].Contains(r2))
				set2.Add(r2)
			Next
			Tests.Assert([set].Equals(set2))

			Dim recsArr As RecordFull() = [set].ToArray()
			Tests.Assert(recsArr.Length = count)
			[set].Clear()
			Tests.Assert([set].Count = 0)
			db.Commit()
			Tests.Assert([set].Count = 0)
			[set].Invalidate()
			[set].Load()
			[set].AddAll(recs)
			Tests.Assert([set].Count = recs.Count)
			db.Commit()
			Tests.Assert([set].Count = recs.Count)
			Tests.Assert([set].GetHashCode() > 0)
			db.Gc()

			' tests for PersistentString
			Dim ps = New PersistentString()
			Tests.Assert(ps.[Get]() = "")
			ps = New PersistentString("Hello")
			Dim s = ps.ToString()
			Tests.Assert(s = "Hello")
			ps.Append("2")
			Tests.Assert("Hello2" = ps.[Get]())
			ps.[Set]("Lala")
			Tests.Assert("Lala" = ps.[Get]())
			Dim s2 As String = ps
			Tests.Assert("Lala" = s2)
			Dim ps2 As PersistentString = "Lulu"
			Tests.Assert(ps2.[Get]() = "Lulu")
			db.Root = ps
			[set].Deallocate()
			db.Gc()
			db.Commit()
			db.Close()
		End Sub

	End Class
End Namespace
