' Copyright: Krzysztof Kowalczyk
' License: BSD
#If WITH_PATRICIA Then
Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestPatriciaTrie
		Implements ITest
		Public Class Root
			Inherits Persistent
			Public idx As IPatriciaTrie(Of RecordFull)
		End Class

		Public Sub Run(config As TestConfig)
			Dim db As IDatabase = config.GetDatabase()
			config.Result = New TestResult()
			Dim root As New Root()
			root.idx = db.CreatePatriciaTrie(Of RecordFull)()
			db.Root = root
			Dim count As Integer = config.Count

			Tests.Assert(0 = root.idx.Count)
			Dim firstKey As Long = 0
			Dim firstRec As RecordFull = Nothing
			Dim pk As PatriciaTrieKey
			Dim r As RecordFull
			For Each key As var In Tests.KeySeq(count)
				r = New RecordFull(key)
				pk = New PatriciaTrieKey(CULng(key), 8)
				root.idx.Add(pk, r)
				If firstRec Is Nothing Then
					firstRec = r
					firstKey = key
				End If
			Next
			Tests.Assert(count = root.idx.Count)
			Tests.Assert(root.idx.Contains(firstRec))
			Tests.Assert(Not root.idx.Contains(New RecordFull(firstKey)))

			pk = New PatriciaTrieKey(CULng(firstKey), 8)
			r = New RecordFull(firstKey)
			Tests.Assert(firstRec = root.idx.Add(pk, r))
			Tests.Assert(r = root.idx.FindExactMatch(pk))
			Tests.Assert(r = root.idx.FindBestMatch(pk))

			For Each key As var In Tests.KeySeq(count)
				pk = New PatriciaTrieKey(CULng(key), 8)
				Tests.Assert(root.idx.Remove(pk) IsNot Nothing)
			Next

			' TODO: seems broken, there's a null entry left
			' in the index
			'foreach (var rf in root.idx)
'            {
'                pk = new PatriciaTrieKey(rf.UInt64Val, 8);
'                Tests.Assert(null != root.idx.Remove(pk));
'            }


			'Tests.Assert(0 == root.idx.Count);
			root.idx.Clear()
			Tests.Assert(0 = root.idx.Count)

			pk = New PatriciaTrieKey(CULng(firstKey), 8)
			Tests.Assert(root.idx.Remove(pk) Is Nothing)

			pk = PatriciaTrieKey.FromIpAddress(New System.Net.IPAddress(123))
			pk = PatriciaTrieKey.FromIpAddress("127.0.0.1")
			pk = PatriciaTrieKey.From7bitString("hola")
			pk = PatriciaTrieKey.From8bitString("hola")
			pk = PatriciaTrieKey.FromByteArray(New Byte(3) {4, 2, 8, 3})
			pk = PatriciaTrieKey.FromDecimalDigits("9834")
			db.Close()
		End Sub
	End Class
End Namespace
#End If
