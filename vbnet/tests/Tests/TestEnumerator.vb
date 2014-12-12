Namespace Volante

	Public Class TestEnumeratorResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public IterationTime As TimeSpan
	End Class

	Public Class TestEnumerator
		Implements ITest
		Private Class Record
			Inherits Persistent
			Friend strKey As [String]
			Friend intKey As Long
		End Class

		Private Class Indices
			Inherits Persistent
			Friend strIndex As IIndex(Of String, Record)
			Friend intIndex As IIndex(Of Long, Record)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestEnumeratorResult()
			config.Result = res

			Dim start = DateTime.Now

			Dim db As IDatabase = config.GetDatabase()
			Dim root As Indices = DirectCast(db.Root, Indices)
			Tests.Assert(root Is Nothing)
			root = New Indices()
			root.strIndex = db.CreateIndex(Of String, Record)(IndexType.NonUnique)
			root.intIndex = db.CreateIndex(Of Long, Record)(IndexType.NonUnique)
			db.Root = root
			Dim intIndex As IIndex(Of Long, Record) = root.intIndex
			Dim strIndex As IIndex(Of String, Record) = root.strIndex
			Dim records As Record()

			Dim key As Long = 1999
			Dim i As Integer, j As Integer
			For i = 0 To count - 1
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
				Dim rec As New Record()
				rec.intKey = key
				rec.strKey = Convert.ToString(key)
				j = CInt(key Mod 10)
				While System.Threading.Interlocked.Decrement(j) >= 0
					intIndex(rec.intKey) = rec
					strIndex(rec.strKey) = rec
				End While
			Next
			db.Commit()
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			key = 1999
			For i = 0 To count - 1
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
				Dim fromInclusive As New Key(key)
				Dim fromInclusiveStr As New Key(Convert.ToString(key))
				Dim fromExclusive As New Key(key, False)
				Dim fromExclusiveStr As New Key(Convert.ToString(key), False)
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
				Dim tillInclusive As New Key(key)
				Dim tillInclusiveStr As New Key(Convert.ToString(key))
				Dim tillExclusive As New Key(key, False)
				Dim tillExclusiveStr As New Key(Convert.ToString(key), False)

				' int key ascent order
				records = intIndex.[Get](fromInclusive, tillInclusive)
				j = 0
				For Each rec As Record In intIndex.Range(fromInclusive, tillInclusive, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](fromInclusive, tillExclusive)
				j = 0
				For Each rec As Record In intIndex.Range(fromInclusive, tillExclusive, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](fromExclusive, tillInclusive)
				j = 0
				For Each rec As Record In intIndex.Range(fromExclusive, tillInclusive, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](fromExclusive, tillExclusive)
				j = 0
				For Each rec As Record In intIndex.Range(fromExclusive, tillExclusive, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](fromInclusive, Nothing)
				j = 0
				For Each rec As Record In intIndex.Range(fromInclusive, Nothing, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](fromExclusive, Nothing)
				j = 0
				For Each rec As Record In intIndex.Range(fromExclusive, Nothing, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](Nothing, tillInclusive)
				j = 0
				For Each rec As Record In intIndex.Range(Nothing, tillInclusive, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.[Get](Nothing, tillExclusive)
				j = 0
				For Each rec As Record In intIndex.Range(Nothing, tillExclusive, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = intIndex.ToArray()
				j = 0
				For Each rec As Record In intIndex
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				' int key descent order
				records = intIndex.[Get](fromInclusive, tillInclusive)
				j = records.Length
				For Each rec As Record In intIndex.Range(fromInclusive, tillInclusive, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](fromInclusive, tillExclusive)
				j = records.Length
				For Each rec As Record In intIndex.Range(fromInclusive, tillExclusive, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](fromExclusive, tillInclusive)
				j = records.Length
				For Each rec As Record In intIndex.Range(fromExclusive, tillInclusive, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](fromExclusive, tillExclusive)
				j = records.Length
				For Each rec As Record In intIndex.Range(fromExclusive, tillExclusive, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](fromInclusive, Nothing)
				j = records.Length
				For Each rec As Record In intIndex.Range(fromInclusive, Nothing, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](fromExclusive, Nothing)
				j = records.Length
				For Each rec As Record In intIndex.Range(fromExclusive, Nothing, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](Nothing, tillInclusive)
				j = records.Length
				For Each rec As Record In intIndex.Range(Nothing, tillInclusive, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.[Get](Nothing, tillExclusive)
				j = records.Length
				For Each rec As Record In intIndex.Range(Nothing, tillExclusive, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = intIndex.ToArray()
				j = records.Length
				For Each rec As Record In intIndex.Reverse()
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				' str key ascent order
				records = strIndex.[Get](fromInclusiveStr, tillInclusiveStr)
				j = 0
				For Each rec As Record In strIndex.Range(fromInclusiveStr, tillInclusiveStr, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](fromInclusiveStr, tillExclusiveStr)
				j = 0
				For Each rec As Record In strIndex.Range(fromInclusiveStr, tillExclusiveStr, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](fromExclusiveStr, tillInclusiveStr)
				j = 0
				For Each rec As Record In strIndex.Range(fromExclusiveStr, tillInclusiveStr, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](fromExclusiveStr, tillExclusiveStr)
				j = 0
				For Each rec As Record In strIndex.Range(fromExclusiveStr, tillExclusiveStr, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](fromInclusiveStr, Nothing)
				j = 0
				For Each rec As Record In strIndex.Range(fromInclusiveStr, Nothing, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](fromExclusiveStr, Nothing)
				j = 0
				For Each rec As Record In strIndex.Range(fromExclusiveStr, Nothing, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](Nothing, tillInclusiveStr)
				j = 0
				For Each rec As Record In strIndex.Range(Nothing, tillInclusiveStr, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.[Get](Nothing, tillExclusiveStr)
				j = 0
				For Each rec As Record In strIndex.Range(Nothing, tillExclusiveStr, IterationOrder.AscentOrder)
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				records = strIndex.ToArray()
				j = 0
				For Each rec As Record In strIndex
					Tests.Assert(rec Is records(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)))
				Next
				Tests.Assert(j = records.Length)

				' str key descent order
				records = strIndex.[Get](fromInclusiveStr, tillInclusiveStr)
				j = records.Length
				For Each rec As Record In strIndex.Range(fromInclusiveStr, tillInclusiveStr, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](fromInclusiveStr, tillExclusiveStr)
				j = records.Length
				For Each rec As Record In strIndex.Range(fromInclusiveStr, tillExclusiveStr, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](fromExclusiveStr, tillInclusiveStr)
				j = records.Length
				For Each rec As Record In strIndex.Range(fromExclusiveStr, tillInclusiveStr, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](fromExclusiveStr, tillExclusiveStr)
				j = records.Length
				For Each rec As Record In strIndex.Range(fromExclusiveStr, tillExclusiveStr, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](fromInclusiveStr, Nothing)
				j = records.Length
				For Each rec As Record In strIndex.Range(fromInclusiveStr, Nothing, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](fromExclusiveStr, Nothing)
				j = records.Length
				For Each rec As Record In strIndex.Range(fromExclusiveStr, Nothing, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](Nothing, tillInclusiveStr)
				j = records.Length
				For Each rec As Record In strIndex.Range(Nothing, tillInclusiveStr, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.[Get](Nothing, tillExclusiveStr)
				j = records.Length
				For Each rec As Record In strIndex.Range(Nothing, tillExclusiveStr, IterationOrder.DescentOrder)
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next
				Tests.Assert(j = 0)

				records = strIndex.ToArray()
				j = records.Length
				For Each rec As Record In strIndex.Reverse()
					Tests.Assert(rec Is records(System.Threading.Interlocked.Decrement(j)))
				Next

				Tests.Assert(j = 0)
			Next
			res.IterationTime = DateTime.Now - start

			strIndex.Clear()
			intIndex.Clear()

			Tests.Assert(Not strIndex.GetEnumerator().MoveNext())
			Tests.Assert(Not intIndex.GetEnumerator().MoveNext())
			Tests.Assert(Not strIndex.Reverse().GetEnumerator().MoveNext())
			Tests.Assert(Not intIndex.Reverse().GetEnumerator().MoveNext())
			db.Commit()
			db.Gc()
			db.Close()
		End Sub
	End Class
End Namespace
