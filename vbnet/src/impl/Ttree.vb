Imports System.Collections
Imports System.Collections.Generic
Imports Volante
Namespace Volante.Impl

	Class Ttree(Of K, V As {Class, IPersistent})
		Inherits PersistentCollection(Of V)
		Implements ISortedCollection(Of K, V)
		Private comparator As PersistentComparator(Of K, V)
		Private unique As Boolean
		Private root As TtreePage(Of K, V)
		Private nMembers As Integer

		Private Sub New()
		End Sub

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return nMembers
			End Get
		End Property

		Public Default ReadOnly Property Item(key As K) As V
			Get
				Return [Get](key)
			End Get
		End Property

		Public Default ReadOnly Property Item(low As K, high As K) As V()
			Get
				Return [Get](low, high)
			End Get
		End Property

		Friend Sub New(comparator As PersistentComparator(Of K, V), unique As Boolean)
			Me.comparator = comparator
			Me.unique = unique
		End Sub

		Public Function GetComparator() As PersistentComparator(Of K, V)
			Return comparator
		End Function

		Public Overrides Function RecursiveLoading() As Boolean
			Return False
		End Function

		Public Function [Get](key As K) As V
			If root Is Nothing Then
				Return Nothing
			End If
			Dim list As New List(Of V)()
			root.find(comparator, key, BoundaryKind.Inclusive, key, BoundaryKind.Inclusive, list)
			If list.Count > 1 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			End If
			If list.Count > 0 Then
				Return list(0)
			End If
			Return Nothing
		End Function

		Public Function [Get](from As K, till As K) As V()
			Return [Get](from, BoundaryKind.Inclusive, till, BoundaryKind.Inclusive)
		End Function

		Public Function [Get](from As K, fromKind As BoundaryKind, till As K, tillKind As BoundaryKind) As V()
			Dim list As New List(Of V)()
			If root IsNot Nothing Then
				root.find(comparator, from, fromKind, till, tillKind, list)
			End If
			Return list.ToArray()
		End Function

		Public Overrides Sub Add(obj As V)
			Dim newRoot As TtreePage(Of K, V) = root
			If root Is Nothing Then
				newRoot = New TtreePage(Of K, V)(obj)
			Else
				If root.insert(comparator, obj, unique, newRoot) = TtreePage(Of K, V).NOT_UNIQUE Then
					Return
				End If
			End If
			Modify()
			root = newRoot
			nMembers += 1
		End Sub

		Public Overrides Function Contains(member As V) As Boolean
			If root Is Nothing Then
				Return False
			End If
			Return root.contains(comparator, member)
		End Function

		Public Overrides Function Remove(obj As V) As Boolean
			If root Is Nothing Then
				Return False
			End If
			' TODO: shouldn't that be an exception too?
			Dim newRoot As TtreePage(Of K, V) = root
			If root.remove(comparator, obj, newRoot) = TtreePage(Of K, V).NOT_FOUND Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If
			Modify()
			root = newRoot
			nMembers -= 1
			Return True
		End Function

		Public Overrides Sub Clear()
			If root Is Nothing Then
				Return
			End If
			root.prune()
			Modify()
			root = Nothing
			nMembers = 0
		End Sub

		Public Overrides Sub Deallocate()
			If root IsNot Nothing Then
				root.prune()
			End If
			MyBase.Deallocate()
		End Sub

		Public Function ToArray() As V()
			Dim arr As V() = New V(nMembers - 1) {}
			If root IsNot Nothing Then
				root.toArray(arr, 0)
			End If
			Return arr
		End Function

		Private Class TtreeEnumerator
			Implements IEnumerator(Of V)
			Implements IEnumerable(Of V)
			Private i As Integer
			Private list As List(Of V)
			'Ttree<K,V>    tree;

			'internal TtreeEnumerator(Ttree<K,V> tree, List<V> list) 
			Friend Sub New(list As List(Of V))
				'this.tree = tree;
				Me.list = list
				i = -1
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of V) Implements IEnumerable(Of V).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return GetEnumerator()
			End Function

			Public Sub Reset() Implements IEnumerator.Reset
				i = -1
			End Sub

			Public ReadOnly Property Current() As V Implements IEnumerator(Of V).Current
				Get
					If i < 0 OrElse i >= list.Count Then
						Throw New InvalidOperationException()
					End If

					Return list(i)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If i + 1 < list.Count Then
					i += 1
					Return True
				End If
				i += 1
				Return False
			End Function
		End Class

		Public Overrides Function GetEnumerator() As IEnumerator(Of V)
			Return GetEnumerator(Nothing, BoundaryKind.None, Nothing, BoundaryKind.None)
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public Function GetEnumerator(from As K, till As K) As IEnumerator(Of V)
			Return Range(from, BoundaryKind.Inclusive, till, BoundaryKind.Inclusive).GetEnumerator()
		End Function

		Public Function Range(from As K, till As K) As IEnumerable(Of V)
			Return Range(from, BoundaryKind.Inclusive, till, BoundaryKind.Inclusive)
		End Function

		Public Function GetEnumerator(from As K, fromKind As BoundaryKind, till As K, tillKind As BoundaryKind) As IEnumerator(Of V)
			Return Range(from, fromKind, till, tillKind).GetEnumerator()
		End Function

		Public Function Range(from As K, fromKind As BoundaryKind, till As K, tillKind As BoundaryKind) As IEnumerable(Of V)
			Dim list As New List(Of V)()
			If root IsNot Nothing Then
				root.find(comparator, from, fromKind, till, tillKind, list)
			End If
			'return new TtreeEnumerator(this, list);
			Return New TtreeEnumerator(list)
		End Function
	End Class
End Namespace
