Imports System.Collections
Namespace Volante

	Public Class TestIndex3
		Implements ITest
		Public Class Record
			Inherits Persistent
			Public strKey As String
			Public intKey As Long
		End Class

		Public Class Root
			Inherits Persistent
			Public strIndex As IIndex(Of String, Record)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestResult()
			config.Result = res
			Dim db As IDatabase = config.GetDatabase()
			Dim root As Root = DirectCast(db.Root, Root)
			Tests.Assert(root Is Nothing)
			root = New Root()
			root.strIndex = db.CreateIndex(Of String, Record)(IndexType.Unique)
			db.Root = root
			Dim strs As String() = New String() {"one", "two", "three", "four"}
			Dim no As Integer = 0
			For i As var = 0 To count - 1
				For Each s As String In strs
					Dim s2 = [String].Format("{0}-{1}", s, i)
					Dim o As New Record()
					o.strKey = s2
					o.intKey = System.Math.Max(System.Threading.Interlocked.Increment(no),no - 1)
					root.strIndex(s2) = o
				Next
			Next
			db.Commit()

			' Test that modyfing an index while traversing it throws an exception
			' Tests Btree.BtreeEnumerator
			Dim n As Long = -1
			Tests.AssertException(Of InvalidOperationException)(Function() 
			For Each r As Record In root.strIndex
				n = r.intKey
				Dim i = n Mod strs.Length
				Dim j = n \ strs.Length
				Dim sBase = strs(i)
				Dim expectedStr = [String].Format("{0}-{1}", sBase, j)
				Dim s As String = r.strKey
				Tests.Assert(s = expectedStr)

				If n = 0 Then
					Dim o As New Record()
					o.strKey = "five"
					o.intKey = 5
					root.strIndex(o.strKey) = o
				End If
			Next

End Function)
			Tests.Assert(n = 0)

			' Test that modyfing an index while traversing it throws an exception
			' Tests Btree.BtreeSelectionIterator

			Dim keyStart As New Key("four", True)
			Dim keyEnd As New Key("three", True)
			Tests.AssertException(Of InvalidOperationException)(Function() 
			For Each r As Record In root.strIndex.Range(keyStart, keyEnd, IterationOrder.AscentOrder)
				n = r.intKey
				Dim i = n Mod strs.Length
				Dim j = n \ strs.Length
				Dim sBase = strs(i)
				Dim expectedStr = [String].Format("{0}-{1}", sBase, j)
				Dim s As String = r.strKey
				Tests.Assert(s = expectedStr)

				Dim o As New Record()
				o.strKey = "six"
				o.intKey = 6
				root.strIndex(o.strKey) = o
			Next

End Function)
			db.Close()
		End Sub
	End Class
End Namespace
