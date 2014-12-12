Imports System.Collections
Namespace Volante

	Public Class TestIndex4
		Implements ITest
		Public Class StringInt
			Inherits Persistent
			Public s As String
			Public no As Integer
			Public Sub New()
			End Sub
			Public Sub New(s As String, no As Integer)
				Me.s = s
				Me.no = no
			End Sub
		End Class

		Public Shared Sub CheckStrings(root As IIndex(Of String, StringInt), strs As String(), count As Integer)
			Dim no As Integer = 1
			For i As var = 0 To count - 1
				For Each s As String In strs
					Dim s2 = [String].Format("{0}-{1}", s, i)
					Dim o As StringInt = root(s2)
					Tests.Assert(o.no = System.Math.Max(System.Threading.Interlocked.Increment(no),no - 1))
				Next
			Next
		End Sub

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestResult()
			config.Result = res
			Dim db As IDatabase = config.GetDatabase()
			Dim root As IIndex(Of String, StringInt) = DirectCast(db.Root, IIndex(Of String, StringInt))
			Tests.Assert(root Is Nothing)
			root = db.CreateIndex(Of String, StringInt)(IndexType.Unique)
			db.Root = root

			Dim strs As String() = New String() {"one", "two", "three", "four"}
			Dim no As Integer = 1
			For i As var = 0 To count - 1
				For Each s As String In strs
					Dim s2 = [String].Format("{0}-{1}", s, i)
					Dim o = New StringInt(s, System.Math.Max(System.Threading.Interlocked.Increment(no),no - 1))
					root(s2) = o
				Next
			Next

			CheckStrings(root, strs, count)
			db.Close()

			db = config.GetDatabase(False)
			root = DirectCast(db.Root, IIndex(Of String, StringInt))
			Tests.Assert(root IsNot Nothing)
			CheckStrings(root, strs, count)
			db.Close()
		End Sub
	End Class
End Namespace
