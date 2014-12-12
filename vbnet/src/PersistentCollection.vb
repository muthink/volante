Imports System.Collections
Imports System.Collections.Generic

Namespace Volante
	''' <summary>
	''' Base class for all persistent collections
	''' </summary>
	Public MustInherit Class PersistentCollection(Of T As {Class, IPersistent})
		Inherits PersistentResource
		Implements ICollection(Of T)
		Public Sub New()
		End Sub

		Public Sub New(db As IDatabase)
			MyBase.New(db)
		End Sub

		Public MustOverride Function GetEnumerator() As IEnumerator(Of T)

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public MustOverride ReadOnly Property Count() As Integer Implements ICollection(Of T).Count

		Public Overridable ReadOnly Property IsSynchronized() As Boolean
			Get
				Return True
			End Get
		End Property

		Public Overridable ReadOnly Property SyncRoot() As Object
			Get
				Return Me
			End Get
		End Property

		Public Overridable Sub CopyTo(dst As T(), i As Integer) Implements ICollection(Of T).CopyTo
			For Each o As Object In Me
				dst.SetValue(o, System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1))
			Next
		End Sub

		Public Overridable Sub Add(obj As T) Implements ICollection(Of T).Add
			Throw New InvalidOperationException("Add is not supported")
		End Sub

		Public MustOverride Sub Clear()

		Public Overridable ReadOnly Property IsReadOnly() As Boolean Implements ICollection(Of T).IsReadOnly
			Get
				Return False
			End Get
		End Property

		Public Overridable Function Contains(obj As T) As Boolean Implements ICollection(Of T).Contains
			For Each o As T In Me
				If o = obj Then
					Return True
				End If
			Next
			Return False
		End Function

		Public Overridable Function Remove(obj As T) As Boolean Implements ICollection(Of T).Remove
			Throw New InvalidOperationException("Remove is not supported")
		End Function
	End Class
End Namespace
