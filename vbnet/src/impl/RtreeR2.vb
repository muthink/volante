Imports System.Collections
Imports System.Collections.Generic
Imports Volante
Namespace Volante.Impl

	Class RtreeR2(Of T As {Class, IPersistent})
		Inherits PersistentCollection(Of T)
		Implements ISpatialIndexR2(Of T)
		Private height As Integer
		Private n As Integer
		Private root As RtreeR2Page
		<NonSerialized> _
		Private updateCounter As Integer

		Friend Sub New()
		End Sub

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return n
			End Get
		End Property

		Public Sub Put(r As RectangleR2, obj As T)
			If root Is Nothing Then
				root = New RtreeR2Page(Database, obj, r)
				height = 1
			Else
				Dim p As RtreeR2Page = root.insert(Database, r, obj, height)
				If p IsNot Nothing Then
					root = New RtreeR2Page(Database, root, p)
					height += 1
				End If
			End If
			n += 1
			updateCounter += 1
			Modify()
		End Sub

		Public Sub Remove(r As RectangleR2, obj As T)
			If root Is Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If
			Dim reinsertList As New ArrayList()
			Dim reinsertLevel As Integer = root.remove(r, obj, height, reinsertList)

			If reinsertLevel < 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If

			Dim i As Integer = reinsertList.Count
			While System.Threading.Interlocked.Decrement(i) >= 0
				Dim p As RtreeR2Page = DirectCast(reinsertList(i), RtreeR2Page)
				Dim j As Integer = 0, pn As Integer = p.n
				While j < pn
					Dim q As RtreeR2Page = root.insert(Database, p.b(j), p.branch(j), height - reinsertLevel)
					If q IsNot Nothing Then
						' root splitted
						root = New RtreeR2Page(Database, root, q)
						height += 1
					End If
					j += 1
				End While
				reinsertLevel -= 1
				p.Deallocate()
			End While
			If root.n = 1 AndAlso height > 1 Then
				Dim newRoot As RtreeR2Page = DirectCast(root.branch(0), RtreeR2Page)
				root.Deallocate()
				root = newRoot
				height -= 1
			End If
			n -= 1
			updateCounter += 1
			Modify()
		End Sub

		Public Function [Get](r As RectangleR2) As T()
			Dim result As New ArrayList()
			If root IsNot Nothing Then
				root.find(r, result, height)
			End If
			Return DirectCast(result.ToArray(GetType(T)), T())
		End Function

		Public ReadOnly Property WrappingRectangle() As RectangleR2
			Get
				Return If((root IsNot Nothing), root.cover(), New RectangleR2(Double.MaxValue, Double.MaxValue, Double.MinValue, Double.MinValue))
			End Get
		End Property

		Public Overrides Sub Clear()
			If root IsNot Nothing Then
				root.purge(height)
				root = Nothing
			End If
			height = 0
			n = 0
			updateCounter += 1
			Modify()
		End Sub

		Public Overrides Sub Deallocate()
			Clear()
			MyBase.Deallocate()
		End Sub

		Public Function Overlaps(r As RectangleR2) As IEnumerable(Of T)
			Return New RtreeIterator(Me, r)
		End Function

		Public Overrides Function GetEnumerator() As IEnumerator(Of T)
			Return Overlaps(WrappingRectangle).GetEnumerator()
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public Function GetDictionaryEnumerator(r As RectangleR2) As IDictionaryEnumerator
			Return New RtreeEntryIterator(Me, r)
		End Function

		Public Function GetDictionaryEnumerator() As IDictionaryEnumerator
			Return GetDictionaryEnumerator(WrappingRectangle)
		End Function

		Private Class RtreeIterator
			Implements IEnumerator(Of T)
			Implements IEnumerable(Of T)
			Friend Sub New(tree As RtreeR2(Of T), r As RectangleR2)
				counter = tree.updateCounter
				height = tree.height
				Me.tree = tree
				If height = 0 Then
					Return
				End If

				Me.r = r
				pageStack = New RtreeR2Page(height - 1) {}
				posStack = New Integer(height - 1) {}
				Reset()
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of T) Implements IEnumerable(Of T).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return GetEnumerator()
			End Function

			Public Sub Reset() Implements IEnumerator.Reset
				hasNext = gotoFirstItem(0, tree.root)
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If counter <> tree.updateCounter Then
					Throw New InvalidOperationException("Tree was modified")
				End If

				If hasNext Then
					page = pageStack(height - 1)
					pos = posStack(height - 1)
					If Not gotoNextItem(height - 1) Then
						hasNext = False
					End If
					Return True
				Else
					page = Nothing
					Return False
				End If
			End Function

			Public Overridable ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					If page Is Nothing Then
						Throw New InvalidOperationException()
					End If
					Return DirectCast(page.branch(pos), T)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Private Function gotoFirstItem(sp As Integer, pg As RtreeR2Page) As Boolean
				Dim i As Integer = 0, n As Integer = pg.n
				While i < n
					If r.Intersects(pg.b(i)) Then
						If sp + 1 = height OrElse gotoFirstItem(sp + 1, DirectCast(pg.branch(i), RtreeR2Page)) Then
							pageStack(sp) = pg
							posStack(sp) = i
							Return True
						End If
					End If
					i += 1
				End While
				Return False
			End Function

			Private Function gotoNextItem(sp As Integer) As Boolean
				Dim pg As RtreeR2Page = pageStack(sp)
				Dim i As Integer = posStack(sp), n As Integer = pg.n
				While System.Threading.Interlocked.Increment(i) < n
					If r.Intersects(pg.b(i)) Then
						If sp + 1 = height OrElse gotoFirstItem(sp + 1, DirectCast(pg.branch(i), RtreeR2Page)) Then
							pageStack(sp) = pg
							posStack(sp) = i
							Return True
						End If
					End If
				End While
				pageStack(sp) = Nothing
				Return If((sp > 0), gotoNextItem(sp - 1), False)
			End Function

			Protected pageStack As RtreeR2Page()
			Protected posStack As Integer()
			Protected counter As Integer
			Protected height As Integer
			Protected pos As Integer
			Protected hasNext As Boolean
			Protected page As RtreeR2Page
			Protected tree As RtreeR2(Of T)
			Protected r As RectangleR2
		End Class

		Private Class RtreeEntryIterator
			Inherits RtreeIterator
			Implements IDictionaryEnumerator
			Friend Sub New(tree As RtreeR2(Of T), r As RectangleR2)
				MyBase.New(tree, r)
			End Sub

			Public Overridable Shadows ReadOnly Property Current() As Object
				Get
					Return Entry
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public ReadOnly Property Entry() As DictionaryEntry Implements IDictionaryEnumerator.Entry
				Get
					If page Is Nothing Then
						Throw New InvalidOperationException()
					End If

					Return New DictionaryEntry(page.b(pos), page.branch(pos))
				End Get
			End Property

			Public ReadOnly Property Key() As Object Implements IDictionaryEnumerator.Key
				Get
					If page Is Nothing Then
						Throw New InvalidOperationException()
					End If

					Return page.b(pos)
				End Get
			End Property

			Public ReadOnly Property Value() As Object Implements IDictionaryEnumerator.Value
				Get
					If page Is Nothing Then
						Throw New InvalidOperationException()
					End If

					Return page.branch(pos)
				End Get
			End Property
		End Class
	End Class
End Namespace

