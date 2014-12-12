Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestIndexResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public IndexSearchTime As TimeSpan
		Public IterationTime As TimeSpan
		Public RemoveTime As TimeSpan
		Public MemoryUsage As ICollection
		' values are of TypeMemoryUsage type
	End Class

	Public Class TestIndex
		Implements ITest
		Public Class Root
			Inherits Persistent
			Public strIndex As IIndex(Of String, RecordFull)
			Public longIndex As IIndex(Of Long, RecordFull)
		End Class

		Private Sub MyCommit(db As IDatabase, serializable As Boolean)
			If serializable Then
				db.EndThreadTransaction()
				db.BeginThreadTransaction(TransactionMode.Serializable)
			Else
				db.Commit()
			End If
		End Sub

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestIndexResult()
			config.Result = res
			Dim db As IDatabase = config.GetDatabase()
			If config.Serializable Then
				db.BeginThreadTransaction(TransactionMode.Serializable)
			End If

			Dim root As Root = DirectCast(db.Root, Root)
			Tests.Assert(root Is Nothing)
			root = New Root()
			root.strIndex = db.CreateIndex(Of String, RecordFull)(IndexType.Unique)
			root.longIndex = db.CreateIndex(Of Long, RecordFull)(IndexType.Unique)
			db.Root = root
			Dim strIndex = root.strIndex
			Tests.Assert(GetType(String) = strIndex.KeyType)
			Dim longIndex = root.longIndex
			Tests.Assert(GetType(Long) = longIndex.KeyType)
			Dim start As DateTime = DateTime.Now
			Dim startWithOne As Integer = 0
			Dim startWithFive As Integer = 0
			Dim strFirst As String = "z"
			Dim strLast As String = "0"

			Dim n As Integer = 0
			For Each key As var In Tests.KeySeq(count)
				Dim rec As New RecordFull(key)
				If rec.StrVal(0) = "1"C Then
					startWithOne += 1
				ElseIf rec.StrVal(0) = "5"C Then
					startWithFive += 1
				End If
				If rec.StrVal.CompareTo(strFirst) < 0 Then
					strFirst = rec.StrVal
				ElseIf rec.StrVal.CompareTo(strLast) > 0 Then
					strLast = rec.StrVal
				End If
				longIndex(rec.Int32Val) = rec
				strIndex(rec.StrVal) = rec
				n += 1
				If n Mod 100 = 0 Then
					MyCommit(db, config.Serializable)
				End If
			Next
			MyCommit(db, config.Serializable)

			Tests.Assert(longIndex.Count = count)
			Tests.Assert(strIndex.Count = count)

			res.InsertTime = DateTime.Now - start
			start = System.DateTime.Now

			For Each key As var In Tests.KeySeq(count)
				Dim rec1 As RecordFull = longIndex(key)
				Dim rec2 As RecordFull = strIndex(Convert.ToString(key))
				Tests.Assert(rec1 IsNot Nothing AndAlso rec2 IsNot Nothing)
				Tests.Assert(rec1 = rec2)
			Next
			res.IndexSearchTime = DateTime.Now - start
			start = System.DateTime.Now

			Dim k = Int64.MinValue
			n = 0
			For Each rec As RecordFull In longIndex
				Tests.Assert(rec.Int32Val >= k)
				k = rec.Int32Val
				n += 1
			Next
			Tests.Assert(n = count)

			Dim strKey As [String] = ""
			n = 0
			For Each rec As RecordFull In strIndex
				Tests.Assert(rec.StrVal.CompareTo(strKey) >= 0)
				strKey = rec.StrVal
				n += 1
			Next
			Tests.Assert(n = count)
			res.IterationTime = DateTime.Now - start
			start = System.DateTime.Now

			Dim de As IDictionaryEnumerator = longIndex.GetDictionaryEnumerator()
			n = VerifyDictionaryEnumerator(de, IterationOrder.AscentOrder)
			Tests.Assert(n = count)

			Dim mid As Long = 0
			Dim max As Long = Long.MaxValue
			de = longIndex.GetDictionaryEnumerator(New Key(mid), New Key(max), IterationOrder.DescentOrder)
			VerifyDictionaryEnumerator(de, IterationOrder.DescentOrder)

			Tests.AssertDatabaseException(Function() longIndex.PrefixSearch("1"), DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			Dim recs As RecordFull() = strIndex.PrefixSearch("1")
			Tests.Assert(startWithOne = recs.Length)
			For Each r As var In recs
				Tests.Assert(r.StrVal.StartsWith("1"))
			Next
			recs = strIndex.PrefixSearch("5")
			Tests.Assert(startWithFive = recs.Length)
			For Each r As var In recs
				Tests.Assert(r.StrVal.StartsWith("5"))
			Next
			recs = strIndex.PrefixSearch("0")
			Tests.Assert(0 = recs.Length)

			recs = strIndex.PrefixSearch("a")
			Tests.Assert(0 = recs.Length)

			recs = strIndex.PrefixSearch(strFirst)
			Tests.Assert(recs.Length >= 1)
			Tests.Assert(recs(0).StrVal = strFirst)
			For Each r As var In recs
				Tests.Assert(r.StrVal.StartsWith(strFirst))
			Next

			recs = strIndex.PrefixSearch(strLast)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(recs(0).StrVal = strLast)

			n = 0
			Dim oneRemoved As RecordFull = Nothing
			For Each key As var In Tests.KeySeq(count)
				n += 1
				If n Mod 3 = 0 Then
					Continue For
				End If
				Dim rec As RecordFull = longIndex.[Get](key)
				Dim removed As RecordFull = longIndex.RemoveKey(key)
				Tests.Assert(removed = rec)
				strIndex.Remove(New Key(System.Convert.ToString(key)), rec)
				Tests.Assert(Not strIndex.Contains(removed))
				If oneRemoved Is Nothing Then
					oneRemoved = removed
				End If
			Next
			db.Rollback()
			'TODO: shouldn't this be true?
			'Tests.Assert(strIndex.Contains(oneRemoved));

			res.RemoveTime = DateTime.Now - start
			db.Close()
			If config.IsTransient Then
				Return
			End If

			db = config.GetDatabase(False)
			root = DirectCast(db.Root, Root)
			longIndex = root.longIndex
			strIndex = root.strIndex
			k = Int64.MinValue
			n = 0
			Dim firstRec As RecordFull = Nothing
			Dim removedRec As RecordFull = Nothing
			For Each rec As RecordFull In longIndex
				Tests.Assert(rec.Int32Val >= k)
				k = rec.Int32Val
				If firstRec Is Nothing Then
					firstRec = rec
				ElseIf removedRec Is Nothing Then
					removedRec = rec
					strIndex.Remove(removedRec.StrVal, removedRec)
				End If
				n += 1
			Next
			Tests.Assert(longIndex.Count = n)
			Tests.Assert(strIndex.Count = n - 1)
			Tests.Assert(longIndex.Contains(firstRec))
			Tests.Assert(strIndex.Contains(firstRec))
			Tests.Assert(Not strIndex.Contains(removedRec))
			Dim notPresent As New RecordFull()
			Tests.Assert(Not strIndex.Contains(notPresent))
			Tests.Assert(Not longIndex.Contains(notPresent))
			longIndex.Clear()
			Tests.Assert(Not longIndex.Contains(firstRec))
			db.Close()
		End Sub

		Private Shared Function VerifyDictionaryEnumerator(de As IDictionaryEnumerator, order As IterationOrder) As Integer
			Dim prev As Long = Long.MinValue
			If order = IterationOrder.DescentOrder Then
				prev = Long.MaxValue
			End If
			Dim i As Integer = 0
			While de.MoveNext()
				Dim e1 As DictionaryEntry = CType(de.Current, DictionaryEntry)
				Dim e2 As DictionaryEntry = de.Entry
				Tests.Assert(e1.Equals(e2))
				Dim k As Long = CLng(e1.Key)
				Dim k2 As Long = CLng(de.Key)
				Tests.Assert(k = k2)
				Dim v1 As RecordFull = DirectCast(e1.Value, RecordFull)
				Dim v2 As RecordFull = DirectCast(de.Value, RecordFull)
				Tests.Assert(v1.Equals(v2))
				Tests.Assert(v1.Int32Val = k)
				If order = IterationOrder.AscentOrder Then
					Tests.Assert(k >= prev)
				Else
					Tests.Assert(k <= prev)
				End If
				prev = k
				i += 1
			End While
			Tests.VerifyDictionaryEnumeratorDone(de)
			Return i
		End Function
	End Class
End Namespace
