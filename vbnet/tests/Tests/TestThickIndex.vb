Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestThickIndex
		Implements ITest
		Public Class Root
			Inherits Persistent
			Public strIdx As IIndex(Of String, RecordFull)
			Public byteIdx As IIndex(Of Byte, RecordFull)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestIndexResult()
			config.Result = res
			Dim db As IDatabase = config.GetDatabase()

			Dim root As Root = DirectCast(db.Root, Root)
			Tests.Assert(root Is Nothing)
			root = New Root()
			root.strIdx = db.CreateThickIndex(Of String, RecordFull)()
			root.byteIdx = db.CreateThickIndex(Of Byte, RecordFull)()
			db.Root = root
			Tests.Assert(GetType(String) = root.strIdx.KeyType)
			Tests.Assert(GetType(Byte) = root.byteIdx.KeyType)

			Dim startWithOne As Integer = 0
			Dim startWithFive As Integer = 0
			Dim strFirst As String = "z"
			Dim strLast As String = "0"

			Dim n As Integer = 0
			Dim inThickIndex = New Dictionary(Of Byte, List(Of RecordFull))()
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
				root.strIdx.Put(rec.StrVal, rec)
				root.byteIdx.Put(rec.ByteVal, rec)
				n += 1
				If n Mod 100 = 0 Then
					db.Commit()
				End If
				

			Next
			db.Commit()

			Tests.Assert(root.strIdx.Count = count)
			Tests.Assert(root.byteIdx.Count <= count)

			Tests.AssertDatabaseException(Function() 
			root.strIdx.RemoveKey("")

End Function, DatabaseException.ErrorCode.KEY_NOT_UNIQUE)

			Tests.AssertDatabaseException(Function() 
			root.strIdx.Remove(New Key(""))

End Function, DatabaseException.ErrorCode.KEY_NOT_UNIQUE)

			For Each mk As var In inThickIndex.Keys
				Dim list = inThickIndex(mk)
				While list.Count > 1
					Dim el = list(0)
					list.Remove(el)
					root.byteIdx.Remove(el.ByteVal, el)
				End While
			Next

			Dim recs As RecordFull()
			For Each key As var In Tests.KeySeq(count)
				Dim rec1 As RecordFull = root.strIdx(Convert.ToString(key))
				recs = root.byteIdx(rec1.ByteVal, rec1.ByteVal)
				Tests.Assert(rec1 IsNot Nothing AndAlso recs.Length >= 1)
				Tests.Assert(rec1.ByteVal = recs(0).ByteVal)
			Next

			' test for non-existent key
			Tests.Assert(root.strIdx.[Get]("-122") Is Nothing)

			recs = root.byteIdx.ToArray()
			Tests.Assert(recs.Length = root.byteIdx.Count)

			Dim prevByte = Byte.MinValue
			n = 0
			For Each rec As RecordFull In root.byteIdx
				Tests.Assert(rec.ByteVal >= prevByte)
				prevByte = rec.ByteVal
				n += 1
			Next
			Tests.Assert(n = count)

			Dim prevStrKey As [String] = ""
			n = 0
			For Each rec As RecordFull In root.strIdx
				Tests.Assert(rec.StrVal.CompareTo(prevStrKey) >= 0)
				prevStrKey = rec.StrVal
				n += 1
			Next
			Dim de As IDictionaryEnumerator = root.strIdx.GetDictionaryEnumerator()
			n = VerifyDictionaryEnumerator(de, IterationOrder.AscentOrder)
			Tests.Assert(n = count)

			Dim mid As String = "0"
			Dim max As String = Long.MaxValue.ToString()
			

			de = root.strIdx.GetDictionaryEnumerator(New Key(mid), New Key(max), IterationOrder.DescentOrder)
			VerifyDictionaryEnumerator(de, IterationOrder.DescentOrder)

			Tests.AssertDatabaseException(Function() root.byteIdx.PrefixSearch("1"), DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			' TODO: FAILED_TEST broken for altbtree, returns no results
			If Not config.AltBtree Then
				recs = root.strIdx.GetPrefix("1")
				Tests.Assert(recs.Length > 0)
				For Each r As var In recs
					Tests.Assert(r.StrVal.StartsWith("1"))
				Next
			End If
			recs = root.strIdx.PrefixSearch("1")
			Tests.Assert(startWithOne = recs.Length)
			For Each r As var In recs
				Tests.Assert(r.StrVal.StartsWith("1"))
			Next
			recs = root.strIdx.PrefixSearch("5")
			Tests.Assert(startWithFive = recs.Length)
			For Each r As var In recs
				Tests.Assert(r.StrVal.StartsWith("5"))
			Next
			recs = root.strIdx.PrefixSearch("0")
			Tests.Assert(0 = recs.Length)

			recs = root.strIdx.PrefixSearch("a")
			Tests.Assert(0 = recs.Length)

			recs = root.strIdx.PrefixSearch(strFirst)
			Tests.Assert(recs.Length >= 1)
			Tests.Assert(recs(0).StrVal = strFirst)
			For Each r As var In recs
				Tests.Assert(r.StrVal.StartsWith(strFirst))
			Next

			recs = root.strIdx.PrefixSearch(strLast)
			Tests.Assert(recs.Length = 1)
			Tests.Assert(recs(0).StrVal = strLast)

			n = 0
			For Each key As var In Tests.KeySeq(count)
				n += 1
				If n Mod 3 = 0 Then
					Continue For
				End If
				Dim strKey As String = key.ToString()
				Dim rec As RecordFull = root.strIdx.[Get](strKey)
				root.strIdx.Remove(strKey, rec)
			Next
			root.byteIdx.Clear()

			Dim BTREE_THRESHOLD As Integer = 128
			Dim bKey As Byte = 1
			For i As Integer = 0 To BTREE_THRESHOLD + 9
				Dim r As New RecordFull(0)
				If i = 0 Then
					root.byteIdx(bKey) = r
					Continue For
				End If
				If i = 1 Then
					root.byteIdx.[Set](bKey, r)
					Continue For
				End If
				root.byteIdx.Put(bKey, r)
			Next

			Tests.AssertDatabaseException(Function() root.byteIdx.[Set](bKey, New RecordFull(1)), DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			Tests.AssertDatabaseException(Function() root.byteIdx.[Get](bKey), DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			recs = root.byteIdx.ToArray()
			For Each r As var In recs
				root.byteIdx.Remove(bKey, r)
			Next
			Tests.AssertDatabaseException(Function() root.byteIdx.Remove(bKey, New RecordFull(0)), DatabaseException.ErrorCode.KEY_NOT_FOUND)
			Dim e As IEnumerator(Of RecordFull) = root.byteIdx.GetEnumerator()
			While e.MoveNext()
			End While
			Tests.Assert(Not e.MoveNext())
			db.Close()
		End Sub

		Private Shared Function VerifyDictionaryEnumerator(de As IDictionaryEnumerator, order As IterationOrder) As Integer
			Dim prev As String = ""
			If order = IterationOrder.DescentOrder Then
				prev = "9999999999999999999"
			End If
			Dim i As Integer = 0
			While de.MoveNext()
				Dim e1 As DictionaryEntry = CType(de.Current, DictionaryEntry)
				Dim e2 As DictionaryEntry = de.Entry
				Tests.Assert(e1.Equals(e2))
				Dim k As String = DirectCast(e1.Key, String)
				Dim k2 As String = DirectCast(de.Key, String)
				Tests.Assert(k = k2)
				Dim v1 As RecordFull = DirectCast(e1.Value, RecordFull)
				Dim v2 As RecordFull = DirectCast(de.Value, RecordFull)
				Tests.Assert(v1.Equals(v2))
				Tests.Assert(v1.StrVal = k)
				If order = IterationOrder.AscentOrder Then
					Tests.Assert(k.CompareTo(prev) >= 0)
				Else
					Tests.Assert(k.CompareTo(prev) <= 0)
				End If
				prev = k
				i += 1
			End While
			Tests.VerifyDictionaryEnumeratorDone(de)
			Return i
		End Function
	End Class
End Namespace
