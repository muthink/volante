#If WITH_OLD_BTREE Then
Imports System.Collections
Imports System.Collections.Generic
Imports Volante

Namespace Volante.Impl
	Class OldPersistentSet(Of T As {Class, IPersistent})
		Inherits OldBtree(Of T, T)
		Implements ISet(Of T)
		Public Sub New()
			MyBase.New(ClassDescriptor.FieldType.tpObject, True)
		End Sub

		Public Overrides Function Contains(o As T) As Boolean Implements ICollection(Of T).Contains
			If Not o.IsPersistent() Then
				Return False
			End If

			Dim key As New Key(o)
			Dim e As IEnumerator(Of T) = GetEnumerator(key, key, IterationOrder.AscentOrder)
			Return e.MoveNext()
		End Function

		Public Overrides Sub Add(o As T) Implements ISet(Of T).Add, ICollection(Of T).Add
			If Not o.IsPersistent() Then
				DirectCast(Database, DatabaseImpl).MakePersistent(o)
			End If
			MyBase.Put(New Key(o), o)
		End Sub

		Public Function AddAll(c As ICollection(Of T)) As Boolean
			Dim modified As Boolean = False
			For Each o As T In c
				If Not o.IsPersistent() Then
					DirectCast(Database, DatabaseImpl).MakePersistent(o)
				End If
				modified = modified Or MyBase.Put(New Key(o), o)
			Next
			Return modified
		End Function

		Public Overrides Function Remove(o As T) As Boolean Implements ICollection(Of T).Remove
			Try
				Remove(New Key(o), o)
			Catch x As DatabaseException
				If x.Code = DatabaseException.ErrorCode.KEY_NOT_FOUND Then
					Return False
				End If

				Throw
			End Try
			Return True
		End Function

		Public Function ContainsAll(c As ICollection(Of T)) As Boolean
			For Each o As T In c
				If Not Contains(o) Then
					Return False
				End If
			Next
			Return True
		End Function

		Public Function RemoveAll(c As ICollection(Of T)) As Boolean
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

			If Count <> s.Count Then
				Return False
			End If

			Return ContainsAll(s)
		End Function

		Public Overrides Function GetHashCode() As Integer
			Dim h As Integer = 0
			For Each o As IPersistent In Me
				h += o.Oid
			Next
			Return h
		End Function
	End Class
End Namespace
#End If
