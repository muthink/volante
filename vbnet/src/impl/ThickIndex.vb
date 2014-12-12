Imports System.Collections
Imports System.Collections.Generic
Imports Volante

Namespace Volante.Impl
	Class ThickIndex(Of K, V As {Class, IPersistent})
		Inherits PersistentCollection(Of V)
		Implements IIndex(Of K, V)
		Private index As IIndex(Of K, IPersistent)
		Private nElems As Integer

		Const BTREE_THRESHOLD As Integer = 128

		Friend Sub New(db As DatabaseImpl)
			MyBase.New(db)
			index = db.CreateIndex(Of K, IPersistent)(IndexType.Unique)
		End Sub

		Private Sub New()
		End Sub

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return nElems
			End Get
		End Property

		Public Default Property Item(key As K) As V
			Get
				Return [Get](key)
			End Get
			Set
				[Set](key, value)
			End Set
		End Property

		Public Default ReadOnly Property Item(from As K, till As K) As V()
			Get
				Return [Get](from, till)
			End Get
		End Property

		Public Function [Get](key As Key) As V
			Dim s As IPersistent = index.[Get](key)
			If s Is Nothing Then
				Return Nothing
			End If

			Dim r As Relation(Of V, V) = TryCast(s, Relation(Of V, V))
			If r IsNot Nothing Then
				If r.Count = 1 Then
					Return r(0)
				End If
			End If
			Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
		End Function

		Public Function [Get](from As Key, till As Key) As V()
			Return extend(index.[Get](from, till))
		End Function

		Public Function [Get](key As K) As V
			Return [Get](KeyBuilder.getKeyFromObject(key))
		End Function

		Public Function [Get](from As K, till As K) As V()
			Return [Get](KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till))
		End Function

		Private Function extend(s As IPersistent()) As V()
			Dim list As New List(Of V)()
			For i As Integer = 0 To s.Length - 1
				list.AddRange(DirectCast(s(i), ICollection(Of V)))
			Next
			Return list.ToArray()
		End Function

		Public Function GetPrefix(prefix As String) As V()
			Return extend(index.GetPrefix(prefix))
		End Function

		Public Function PrefixSearch(word As String) As V()
			Return extend(index.PrefixSearch(word))
		End Function

		Public Overrides Sub Clear()
			' TODO: not sure but the index might not own the objects in it,
			' so it cannot deallocate them
			'foreach (IPersistent o in this)
			'{
			'    o.Deallocate();
			'}
			index.Clear()
			nElems = 0
			Modify()
		End Sub

		Public Function ToArray() As V()
			Return extend(index.ToArray())
		End Function

		Private Class ExtendEnumerator
			Implements IEnumerator(Of V)
			Implements IEnumerable(Of V)
			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If reachedEnd Then
					Return False
				End If
				While Not inner.MoveNext()
					If Not outer.MoveNext() Then
						reachedEnd = False
						Return False
					End If
					inner = DirectCast(outer.Current, IEnumerable(Of V)).GetEnumerator()
				End While
				Return True
			End Function

			Public ReadOnly Property Current() As V Implements IEnumerator(Of V).Current
				Get
					If reachedEnd Then
						Throw New InvalidOperationException()
					End If
					Return inner.Current
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					If reachedEnd Then
						Throw New InvalidOperationException()
					End If
					Return Current
				End Get
			End Property

			Public Sub Reset() Implements IEnumerator.Reset
				reachedEnd = True
				If outer.MoveNext() Then
					reachedEnd = False
					inner = DirectCast(outer.Current, IEnumerable(Of V)).GetEnumerator()
				End If
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of V) Implements IEnumerable(Of V).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return Me
			End Function

			Friend Sub New(enumerator As IEnumerator(Of IPersistent))
				outer = enumerator
				Reset()
			End Sub

			Private outer As IEnumerator(Of IPersistent)
			Private inner As IEnumerator(Of V)
			Private reachedEnd As Boolean
		End Class

		Private Class ExtendDictionaryEnumerator
			Implements IDictionaryEnumerator
			Public ReadOnly Property Current() As Object Implements IEnumerator.Current
				Get
					Return Entry
				End Get
			End Property

			Public ReadOnly Property Entry() As DictionaryEntry Implements IDictionaryEnumerator.Entry
				Get
					Return New DictionaryEntry(m_key, inner.Current)
				End Get
			End Property

			Public ReadOnly Property Key() As Object Implements IDictionaryEnumerator.Key
				Get
					If reachedEnd Then
						Throw New InvalidOperationException()
					End If
					Return m_key
				End Get
			End Property

			Public ReadOnly Property Value() As Object Implements IDictionaryEnumerator.Value
				Get
					Return inner.Current
				End Get
			End Property

			Public Sub Dispose()
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If reachedEnd Then
					Return False
				End If

				While Not inner.MoveNext()
					If Not outer.MoveNext() Then
						reachedEnd = True
						Return False
					End If

					m_key = outer.Key
					inner = DirectCast(outer.Value, IEnumerable(Of V)).GetEnumerator()
				End While
				Return True
			End Function

			Public Sub Reset() Implements IEnumerator.Reset
				reachedEnd = True
				If outer.MoveNext() Then
					reachedEnd = False
					m_key = outer.Key
					inner = DirectCast(outer.Value, IEnumerable(Of V)).GetEnumerator()
				End If
			End Sub

			Friend Sub New(enumerator As IDictionaryEnumerator)
				outer = enumerator
				Reset()
			End Sub

			Private outer As IDictionaryEnumerator
			Private inner As IEnumerator(Of V)
			Private m_key As Object
			Private reachedEnd As Boolean
		End Class

		Public Overridable Function GetDictionaryEnumerator() As IDictionaryEnumerator
			Return New ExtendDictionaryEnumerator(index.GetDictionaryEnumerator())
		End Function

		Public Overrides Function GetEnumerator() As IEnumerator(Of V)
			Return New ExtendEnumerator(index.GetEnumerator())
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

        Public Overloads Function GetEnumerator(from As Key, till As Key, order As IterationOrder) As IEnumerator(Of V)
            Return Range(from, till, order).GetEnumerator()
        End Function

        Public Overloads Function GetEnumerator(from As K, till As K, order As IterationOrder) As IEnumerator(Of V)
            Return Range(from, till, order).GetEnumerator()
        End Function

        Public Overloads Function GetEnumerator(from As Key, till As Key) As IEnumerator(Of V)
            Return Range(from, till).GetEnumerator()
        End Function

        Public Overloads Function GetEnumerator(from As K, till As K) As IEnumerator(Of V)
            Return Range(from, till).GetEnumerator()
        End Function

        Public Overloads Function GetEnumerator(prefix As String) As IEnumerator(Of V)
            Return StartsWith(prefix).GetEnumerator()
        End Function

		Public Overridable Function Range(from As Key, till As Key, order As IterationOrder) As IEnumerable(Of V)
			Return New ExtendEnumerator(index.GetEnumerator(from, till, order))
		End Function

		Public Overridable Function Reverse() As IEnumerable(Of V)
			Return New ExtendEnumerator(index.Reverse().GetEnumerator())
		End Function

		Public Overridable Function Range(from As Key, till As Key) As IEnumerable(Of V)
			Return New ExtendEnumerator(index.GetEnumerator(from, till))
		End Function

		Public Function Range(from As K, till As K, order As IterationOrder) As IEnumerable(Of V)
			Return New ExtendEnumerator(index.GetEnumerator(from, till, order))
		End Function

		Public Function Range(from As K, till As K) As IEnumerable(Of V)
			Return New ExtendEnumerator(index.GetEnumerator(from, till))
		End Function

		Public Function StartsWith(prefix As String) As IEnumerable(Of V)
			Return New ExtendEnumerator(index.GetEnumerator(prefix))
		End Function

		Public Overridable Function GetDictionaryEnumerator(from As Key, till As Key, order As IterationOrder) As IDictionaryEnumerator
			Return New ExtendDictionaryEnumerator(index.GetDictionaryEnumerator(from, till, order))
		End Function

		Public ReadOnly Property KeyType() As Type
			Get
				Return index.KeyType
			End Get
		End Property

		Public Function Put(key As Key, obj As V) As Boolean
			Dim s As IPersistent = index.[Get](key)
			If s Is Nothing Then
				Dim r As Relation(Of V, V) = Database.CreateRelation(Of V, V)(Nothing)
				r.Add(obj)
				index.Put(key, r)
			ElseIf TypeOf s Is Relation(Of V, V) Then
				Dim r As Relation(Of V, V) = DirectCast(s, Relation(Of V, V))
				If r.Count = BTREE_THRESHOLD Then
					Dim ps As ISet(Of V) = DirectCast(Database, DatabaseImpl).CreateBtreeSet(Of V)()
					For i As Integer = 0 To BTREE_THRESHOLD - 1
						ps.Add(r(i))
					Next
					ps.Add(obj)
					index.[Set](key, ps)
					r.Deallocate()
				Else
					r.Add(obj)
				End If
			Else
				DirectCast(s, ISet(Of V)).Add(obj)
			End If
			nElems += 1
			Modify()
			Return True
		End Function

		Public Function [Set](key As Key, obj As V) As V
			Dim s As IPersistent = index.[Get](key)
			If s Is Nothing Then
				Dim r As Relation(Of V, V) = Database.CreateRelation(Of V, V)(Nothing)
				r.Add(obj)
				index.Put(key, r)
				nElems += 1
				Modify()
				Return Nothing
			ElseIf TypeOf s Is Relation(Of V, V) Then
				Dim r As Relation(Of V, V) = DirectCast(s, Relation(Of V, V))
				If r.Count = 1 Then
					Dim prev As V = r(0)
					r(0) = obj
					Return prev
				End If
			End If
			Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
		End Function

		Public Sub Remove(key As Key, obj As V)
			Dim s As IPersistent = index.[Get](key)
			If TypeOf s Is Relation(Of V, V) Then
				Dim r As Relation(Of V, V) = DirectCast(s, Relation(Of V, V))
				Dim i As Integer = r.IndexOf(obj)
				If i >= 0 Then
					r.RemoveAt(i)
					If r.Count = 0 Then
						index.Remove(key, r)
						r.Deallocate()
					End If
					nElems -= 1
					Modify()
					Return
				End If
			ElseIf TypeOf s Is ISet(Of V) Then
				Dim ps As ISet(Of V) = DirectCast(s, ISet(Of V))
				If ps.Remove(obj) Then
					If ps.Count = 0 Then
						index.Remove(key, ps)
						ps.Deallocate()
					End If
					nElems -= 1
					Modify()
					Return
				End If
			End If
			Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
		End Sub

        Public Overloads Function Remove(key As Key) As V Implements IIndex(Of K, V).Remove
            Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
        End Function

		Public Function Put(key As K, obj As V) As Boolean
			Return Put(KeyBuilder.getKeyFromObject(key), obj)
		End Function

		Public Function [Set](key As K, obj As V) As V
			Return [Set](KeyBuilder.getKeyFromObject(key), obj)
		End Function

        Public Overloads Sub Remove(key As K, obj As V) Implements IIndex(Of K, V).Remove
            Remove(KeyBuilder.getKeyFromObject(key), obj)
        End Sub

		Public Function RemoveKey(key As K) As V
			Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
		End Function

		Public Overrides Sub Deallocate()
			Clear()
			index.Deallocate()
			MyBase.Deallocate()
		End Sub
	End Class
End Namespace
