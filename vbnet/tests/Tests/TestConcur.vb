Imports System.Threading
Namespace Volante

	Public Class TestConcurResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public AccessTime As TimeSpan
	End Class

	Public Class TestConcur
		Implements ITest
		Private Class L2List
			Inherits PersistentResource
			Friend head As L2Elem
		End Class

		Private Class L2Elem
			Inherits Persistent
			Friend [next] As L2Elem
			Friend prev As L2Elem
			Friend count As Integer

			Public Overrides Function RecursiveLoading() As Boolean
				Return False
			End Function

			Friend Sub unlink()
				[next].prev = prev
				prev.[next] = [next]
				[next].Store()
				prev.Store()
			End Sub

			Friend Sub linkAfter(elem As L2Elem)
				elem.[next].prev = Me
				[next] = elem.[next]
				elem.[next] = Me
				prev = elem
				Store()
				[next].Store()
				prev.Store()
			End Sub
		End Class

		Const nIterations As Integer = 100
		Const nThreads As Integer = 4
		Shared nElements As Integer = 0

		Shared db As IDatabase
		#If CF Then
		Shared nFinishedThreads As Integer
		#End If
		Public Shared Sub run()
			Dim list As L2List = DirectCast(db.Root, L2List)
			For i As Integer = 0 To nIterations - 1
				Dim sum As Long = 0, n As Long = 0
				list.SharedLock()
				Dim head As L2Elem = list.head
				Dim elem As L2Elem = head
				Do
					elem.Load()
					sum += elem.count
					n += 1
				Loop While (InlineAssignHelper(elem, elem.[next])) IsNot head
				Tests.Assert(n = nElements AndAlso sum = CLng(nElements) * (nElements - 1) \ 2)
				list.Unlock()
				list.ExclusiveLock()
				Dim last As L2Elem = list.head.prev
				last.unlink()
				last.linkAfter(list.head)
				list.Unlock()
			Next
			#If CF Then
			SyncLock GetType(TestConcur)
				If System.Threading.Interlocked.Increment(nFinishedThreads) = nThreads Then
					db.Close()
				End If
			End SyncLock
			#End If
		End Sub

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestConcurResult()
			config.Result = res

			TestConcur.nElements = count
			Dim start = DateTime.Now

			db = config.GetDatabase()
			Dim list As L2List = DirectCast(db.Root, L2List)
			Tests.Assert(list Is Nothing)
			list = New L2List()
			list.head = New L2Elem()
			list.head.[next] = InlineAssignHelper(list.head.prev, list.head)
			db.Root = list
			For i As Integer = 1 To nElements - 1
				Dim elem As New L2Elem()
				elem.count = i
				elem.linkAfter(list.head)
			Next
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			Dim threads As Thread() = New Thread(nThreads - 1) {}
			For i As Integer = 0 To nThreads - 1
				threads(i) = New Thread(New ThreadStart(AddressOf run))
				threads(i).Start()
			Next
			#If Not CF Then
			For i As Integer = 0 To nThreads - 1
				threads(i).Join()
			Next
			#End If
			db.Close()
			res.AccessTime = DateTime.Now - start
		End Sub
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class

End Namespace
