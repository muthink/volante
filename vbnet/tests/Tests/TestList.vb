Namespace Volante

	Public Class TestListResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public TraverseReadTime As TimeSpan
		Public TraverseModifyTime As TimeSpan
		Public InsertTime4 As TimeSpan
	End Class

	Public Class TestList
		Implements ITest
		Public MustInherit Class LinkNode
			Inherits Persistent
			Public MustOverride Property Number() As Integer

			Public MustOverride Property [Next]() As LinkNode
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestListResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			db.Root = db.CreateClass(GetType(LinkNode))
			Dim header As LinkNode = DirectCast(db.Root, LinkNode)
			Dim current As LinkNode
			current = header
			For i As Integer = 0 To count - 1
				current.[Next] = DirectCast(db.CreateClass(GetType(LinkNode)), LinkNode)
				current = current.[Next]
				current.Number = i
			Next
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			Dim number As Integer = 0
			' A variable used to validate the data in list
			current = header
			While current.[Next] IsNot Nothing
				' Traverse the whole list in the database
				current = current.[Next]
				Tests.Assert(current.Number = System.Math.Max(System.Threading.Interlocked.Increment(number),number - 1))
			End While
			res.TraverseReadTime = DateTime.Now - start

			start = DateTime.Now
			number = 0
			current = header
			While current.[Next] IsNot Nothing
				' Traverse the whole list in the database
				current = current.[Next]
				Tests.Assert(current.Number = System.Math.Max(System.Threading.Interlocked.Increment(number),number - 1))
				current.Number = -current.Number
			End While
			res.TraverseModifyTime = DateTime.Now - start
			db.Close()
		End Sub
	End Class

	Public Class TestL2List
		Implements ITest
		Public Class Record
			Inherits L2ListElem(Of Record)
			Public v As Long
			Public s As String

			Public Sub New()
			End Sub

			Public Sub New(n As Long)
				v = n
				s = n.ToString()
			End Sub
		End Class

		Public Class Root
			Inherits Persistent
			Public l As L2List(Of Record)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestListResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Dim root = New Root()
			root.l = New L2List(Of Record)()
			Dim l = root.l
			db.Root = root

			Tests.Assert(l.Head Is Nothing)
			Tests.Assert(l.Tail Is Nothing)
			Tests.Assert(0 = l.Count)
			For Each k As var In Tests.KeySeq(count)
				Dim r As New Record(k)
				If k Mod 3 Is 0 Then
					l.Append(r)
				ElseIf k Mod 3 Is 1 Then
					l.Prepend(r)
				Else
					l.Add(r)
				End If
			Next
			Tests.Assert(count = l.Count)
			Tests.Assert(l.Head IsNot Nothing)
			Tests.Assert(l.Tail IsNot Nothing)
			Tests.Assert(l.Contains(l.Head))
			Tests.Assert(l.Contains(l.Tail))
			Tests.Assert(Not l.Contains(New Record(-1234)))

			Dim e = l.GetEnumerator()
			Dim rFirst As Record = Nothing
			While e.MoveNext()
				Tests.Assert(e.Current IsNot Nothing)
				If rFirst Is Nothing Then
					rFirst = e.Current
					Tests.Assert(rFirst.Prev Is Nothing)
					Tests.Assert(rFirst.[Next] IsNot Nothing)
				End If
			End While
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim tmp = e.Current

End Function)
			Tests.Assert(Not e.MoveNext())
			e.Reset()
			Tests.Assert(e.MoveNext())

			l.Remove(l.Head)
			l.Remove(l.Tail)

			l.Clear()
			Tests.Assert(0 = l.Count)
			Dim rTmp = New Record(0)
			l.Add(rTmp)
			Tests.Assert(rTmp = l.Head)
			Tests.Assert(rTmp = l.Tail)
			Dim rTmp2 = New Record(1)
			l.Add(rTmp2)
			Tests.Assert(rTmp2 = l.Tail)
			Tests.Assert(rTmp = l.Head)
			Tests.Assert(2 = l.Count)
			db.Commit()
			db.Close()
		End Sub

	End Class
End Namespace
