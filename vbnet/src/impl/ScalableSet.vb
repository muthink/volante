Imports System.Collections
Imports System.Collections.Generic

Namespace Volante.Impl
	Class ScalableSet(Of T As {Class, IPersistent})
		Inherits PersistentCollection(Of T)
		Implements ISet(Of T)
		Private link As ILink(Of T)
		Private pset As ISet(Of T)
		Const BTREE_THRESHOLD As Integer = 128

		Friend Sub New(db As DatabaseImpl, initialSize As Integer)
			MyBase.New(db)
			If initialSize <= BTREE_THRESHOLD Then
				link = db.CreateLink(Of T)(initialSize)
			Else
				pset = db.CreateBtreeSet(Of T)()
			End If
		End Sub

		Private Sub New()
		End Sub

		Public Overrides ReadOnly Property Count() As Integer Implements ICollection(Of T).Count
			Get
				If pset IsNot Nothing Then
					Return pset.Count
				Else
					Return link.Count
				End If
			End Get
		End Property

		Public Overrides Sub Clear() Implements ICollection(Of T).Clear
			If pset IsNot Nothing Then
				pset.Clear()
			Else
				link.Clear()
				Modify()
			End If
		End Sub

		Public Overrides Function Contains(o As T) As Boolean Implements ICollection(Of T).Contains
			If pset IsNot Nothing Then
				Return pset.Contains(o)
			Else
				Return link.Contains(o)
			End If
		End Function

		Public Function ToArray() As T()
			If pset IsNot Nothing Then
				Return pset.ToArray()
			Else
				Return link.ToArray()
			End If
		End Function

		Public Overrides Function GetEnumerator() As IEnumerator(Of T) Implements IEnumerable(Of T).GetEnumerator
			If pset IsNot Nothing Then
				Return pset.GetEnumerator()
			Else
				Return link.GetEnumerator()
			End If
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public Overrides Sub Add(o As T) Implements ISet(Of T).Add, ICollection(Of T).Add
			If pset IsNot Nothing Then
				pset.Add(o)
				Return
			End If

			If link.IndexOf(o) >= 0 Then
				Return
			End If

			If link.Count <= BTREE_THRESHOLD Then
				Modify()
				link.Add(o)
				Return
			End If

			pset = DirectCast(Database, DatabaseImpl).CreateBtreeSet(Of T)()
			Dim i As Integer = 0, n As Integer = link.Count
			While i < n
				pset.Add(link(i))
				i += 1
			End While
			link = Nothing
			Modify()
			pset.Add(o)
		End Sub

		Public Overrides Function Remove(o As T) As Boolean Implements ICollection(Of T).Remove
			If pset IsNot Nothing Then
				Return pset.Remove(o)
			End If

			Dim i As Integer = link.IndexOf(o)
			If i < 0 Then
				Return False
			End If

			link.RemoveAt(i)
			Modify()
			Return True
		End Function

		Public Function ContainsAll(c As ICollection(Of T)) As Boolean
			If pset IsNot Nothing Then
				Return pset.ContainsAll(c)
			End If

			For Each o As T In c
				If Not Contains(o) Then
					Return False
				End If
			Next
			Return True
		End Function

		Public Function AddAll(c As ICollection(Of T)) As Boolean
			If pset IsNot Nothing Then
				Return pset.AddAll(c)
			End If

			Dim modified As Boolean = False
			For Each o As T In c
				If Not Contains(o) Then
					modified = True
					Add(o)
				End If
			Next
			Return modified
		End Function

		Public Function RemoveAll(c As ICollection(Of T)) As Boolean
			If pset IsNot Nothing Then
				Return pset.RemoveAll(c)
			End If

			Dim modified As Boolean = False
			For Each o As T In c
				modified = modified Or Remove(o)
			Next
			Return modified
		End Function

		Public Overrides Function Equals(o As Object) As Boolean
			If o Is Me Then
				Return True
			End If

			Dim s As ISet(Of T) = TryCast(o, ISet(Of T))
			If s Is Nothing Then
				Return False
			End If

			If s.Count <> Count Then
				Return False
			End If

			Return ContainsAll(s)
		End Function

		Public Overrides Function GetHashCode() As Integer
			If pset IsNot Nothing Then
				Return pset.GetHashCode()
			End If

			Dim h As Integer = 0
			For Each o As IPersistent In Me
				h += o.Oid
			Next
			Return h
		End Function

		Public Overrides Sub Deallocate()
			If pset IsNot Nothing Then
				pset.Deallocate()
			End If

			MyBase.Deallocate()
		End Sub
	End Class
End Namespace
