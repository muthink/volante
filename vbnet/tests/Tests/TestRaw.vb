Imports System.Collections
Imports System.Diagnostics
Namespace Volante

	Public Class TestRawResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public TraverseTime As TimeSpan
	End Class

	<Serializable> _
	Class L1List
		Friend [next] As L1List
		Friend obj As [Object]

		Friend Sub New(val As [Object], list As L1List)
			obj = val
			[next] = list
		End Sub
	End Class

	Public Class TestRaw
		Inherits Persistent
		Implements ITest
		Private list As L1List
		Private map As Hashtable
		Private nil As [Object]

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestRawResult()
			config.Result = res

			Dim nHashMembers As Integer = count * 10
			Dim start = DateTime.Now

			Dim db As IDatabase = config.GetDatabase()
			Dim root As TestRaw = DirectCast(db.Root, TestRaw)
			If count Mod 2 <> 0 Then
				' Silence compiler about unused nil variable.
				' This shouldn't happen since we never pass count
				' that is an odd number
				Debug.Assert(False)
				root.nil = New Object()
			End If

			root = New TestRaw()
			Tests.Assert(root.nil Is Nothing)
			Dim list As L1List = Nothing
			For i As Integer = 0 To count - 1
				list = New L1List(i, list)
			Next
			root.list = list
			root.map = New Hashtable()
			For i As Integer = 0 To nHashMembers - 1
				root.map("key-" & i) = "value-" & i
			Next
			db.Root = root
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			Dim elem As L1List = root.list
			Dim i As Integer = count
			While System.Threading.Interlocked.Decrement(i) >= 0
				Tests.Assert(elem.obj.Equals(i))
				elem = elem.[next]
			End While
			Dim i As Integer = nHashMembers
			While System.Threading.Interlocked.Decrement(i) >= 0
				Tests.Assert(root.map("key-" & i).Equals("value-" & i))
			End While
			res.TraverseTime = DateTime.Now - start
			db.Close()
		End Sub
	End Class
End Namespace
