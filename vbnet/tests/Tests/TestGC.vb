Imports System.Collections.Generic
Namespace Volante

	Public Class TestGcResult
		Inherits TestResult
	End Class

	Public Class TestGc
		Implements ITest
		Private Class PObject
			Inherits Persistent
			Friend intKey As Long
			Friend [next] As PObject
			Friend strKey As [String]
		End Class

		Private Class Root
			Inherits Persistent
			Friend list As PObject
			Friend strIndex As IIndex(Of String, PObject)
			Friend intIndex As IIndex(Of Long, PObject)
		End Class

		Const nObjectsInTree As Integer = 10000

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestGcResult()
			config.Result = res
			Dim db As IDatabase = config.GetDatabase()
			Dim root As New Root()
			Dim strIndex As IIndex(Of String, PObject) = InlineAssignHelper(root.strIndex, db.CreateIndex(Of String, PObject)(IndexType.Unique))
			Dim intIndex As IIndex(Of Long, PObject) = InlineAssignHelper(root.intIndex, db.CreateIndex(Of Long, PObject)(IndexType.Unique))
			db.Root = root
			Dim insKey As Long = 1999
			Dim remKey As Long = 1999

			For i As Integer = 0 To count - 1
				If i > nObjectsInTree Then
					remKey = (3141592621L * remKey + 2718281829L) Mod 1000000007L
					intIndex.Remove(New Key(remKey))
					strIndex.Remove(New Key(remKey.ToString()))
				End If
				Dim obj As New PObject()
				insKey = (3141592621L * insKey + 2718281829L) Mod 1000000007L
				obj.intKey = insKey
				obj.strKey = insKey.ToString()
				obj.[next] = New PObject()
				intIndex(obj.intKey) = obj
				strIndex(obj.strKey) = obj
				If i > 0 Then
					Tests.Assert(root.list.intKey = i - 1)
				End If
				root.list = New PObject()
				root.list.intKey = i
				root.Store()
				If i Mod 1000 = 0 Then
					db.Commit()
				End If
			Next
			db.Close()
		End Sub
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
