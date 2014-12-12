Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestProjection
		Implements ITest
		Public Class ToRec
			Inherits Persistent
			Private v As Integer

			Public Sub New()
			End Sub

			Public Sub New(v As Integer)
				Me.v = v
			End Sub
		End Class

		Public Class FromRec
			Inherits Persistent
			Public v As Long
			Public list As ILink(Of ToRec)
			Public list2 As ToRec()
			Public toEl As ToRec

			Public Sub New()
			End Sub

			Public Sub New(db As IDatabase, v As Long)
				Me.v = v
				Dim n As Integer = 5
				list = db.CreateLink(Of ToRec)(n)
				list.Length = n
				list2 = New ToRec(n - 1) {}
				For i As Integer = 0 To n - 1
					list(i) = New ToRec(i)
					list2(i) = New ToRec(i)
				Next
				toEl = New ToRec(n)
			End Sub
		End Class

		Public Class Root
			Inherits Persistent
			Public arr As IPArray(Of FromRec)
		End Class

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim db As IDatabase = config.GetDatabase()
			config.Result = New TestResult()
			Dim root As New Root()
			Dim arr = db.CreateArray(Of FromRec)(45)
			root.arr = arr
			db.Root = root
			db.Commit()
			Dim projectedEls As Integer = 45
			For i = 0 To projectedEls - 1
				arr.Add(New FromRec(db, i))
			Next
			arr(0).toEl = Nothing

			db.Commit()
			Dim p1 = New Projection(Of FromRec, ToRec)("list")
			Tests.Assert(p1.Count = 0)
			Tests.Assert(p1.Length = 0)
			Tests.Assert(Not p1.IsReadOnly)
			Tests.Assert(Not p1.IsSynchronized)
			Tests.Assert(p1.SyncRoot Is Nothing)
			p1.Project(arr)
			Tests.Assert(p1.Count = projectedEls * 5)
			Dim arrTmp = p1.ToArray()
			Tests.Assert(arrTmp.Length = p1.Length)
			p1.Reset()

			p1.Project(arr(0))
			Tests.Assert(p1.Length = 5)
			p1.Reset()

			Dim arr3 = arr.ToArray()
			p1.Project(arr3)
			Tests.Assert(p1.Length = projectedEls * 5)
			p1.Clear()

			Dim e1 As IEnumerator(Of FromRec) = arr.GetEnumerator()
			p1.Project(e1)
			Tests.Assert(p1.Length = projectedEls * 5)

			Dim p2 = New Projection(Of FromRec, ToRec)("list2")
			p2.Project(arr)
			Tests.Assert(p2.Length = projectedEls * 5)

			Dim p3 = New Projection(Of FromRec, ToRec)("toEl")
			p3.Project(arr)
			Tests.Assert(p2.Length = projectedEls * 5)

			p1.Join(Of FromRec)(p2)

			Tests.Assert(p1.GetEnumerator() IsNot Nothing)
			Dim eTmp As IEnumerator = DirectCast(p1, IEnumerable).GetEnumerator()
			Tests.Assert(eTmp IsNot Nothing)

			Dim res As ToRec() = New ToRec(p3.Count - 1) {}
			p3.CopyTo(res, 0)
			For Each tmp As var In res
				Tests.Assert(p3.Contains(tmp))
				p3.Remove(tmp)
				Tests.Assert(Not p3.Contains(tmp))
			Next
			Tests.Assert(0 = p3.Length)
			db.Commit()
			db.Close()
		End Sub
	End Class
End Namespace
