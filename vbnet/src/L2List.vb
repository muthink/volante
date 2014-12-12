Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	''' <summary>
	''' Double linked list.
	''' </summary>
	Public Class L2List(Of T As L2ListElem(Of T))
		Inherits PersistentCollection(Of T)
		Private m_head As T
		Private m_tail As T

		Private nElems As Integer
		Private updateCounter As Integer

		''' <summary>
		''' Get list head element
		''' </summary>
		''' <returns>list head element or null if list is empty
		''' </returns>>
		Public ReadOnly Property Head() As T
			Get
				Return m_head
			End Get
		End Property

		''' <summary>
		''' Get list tail element
		''' </summary>
		''' <returns>list tail element or null if list is empty
		''' </returns>
		Public ReadOnly Property Tail() As T
			Get
				Return m_tail
			End Get
		End Property

		Public Overrides Function Contains(obj As T) As Boolean
			For Each o As T In Me
				If o = obj Then
					Return True
				End If
			Next
			Return False
		End Function

		''' <summary>
		''' Make list empty. 
		''' </summary>
		Public Overrides Sub Clear()
			SyncLock Me
				Modify()
				m_head = InlineAssignHelper(m_tail, Nothing)
				nElems = 0
				updateCounter += 1
			End SyncLock
		End Sub

		''' <summary>
		''' Insert element at the beginning of the list
		''' </summary>
		Public Sub Prepend(elem As T)
			SyncLock Me
				Modify()
				elem.Modify()
				elem.[next] = m_head
				elem.prev = Nothing
				If m_head IsNot Nothing Then
					m_head.Modify()
					m_head.prev = elem
				Else
					m_tail = elem
				End If

				m_head = elem
				nElems += 1
				updateCounter += 1
			End SyncLock
		End Sub

		''' <summary>
		''' Insert element at the end of the list
		''' </summary>
		Public Sub Append(elem As T)
			SyncLock Me
				Modify()
				elem.Modify()
				elem.[next] = Nothing
				elem.prev = m_tail
				If m_tail IsNot Nothing Then
					m_tail.Modify()
					m_tail.[next] = elem
				Else
					m_tail = elem
				End If

				m_tail = elem
				If m_head Is Nothing Then
					m_head = elem
				End If
				nElems += 1
				updateCounter += 1
			End SyncLock
		End Sub

		''' <summary>
		''' Remove element from the list
		''' </summary>
		Public Overrides Function Remove(elem As T) As Boolean
			SyncLock Me
				Modify()
				If elem.prev IsNot Nothing Then
					elem.prev.Modify()
					elem.prev.[next] = elem.[next]
					elem.prev = Nothing
				Else
					m_head = m_head.[next]
				End If

				If elem.[next] IsNot Nothing Then
					elem.[next].Modify()
					elem.[next].prev = elem.prev
					elem.[next] = Nothing
				Else
					m_tail = m_tail.prev
				End If

				nElems -= 1
				updateCounter += 1
				Return True
			End SyncLock
		End Function

		''' <summary>
		''' Add element to the list
		''' </summary>
		Public Overrides Sub Add(elem As T)
			Append(elem)
		End Sub

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return nElems
			End Get
		End Property

		Private Class L2ListEnumerator
			Implements IEnumerator(Of T)
			Private curr As T
			Private counter As Integer
			Private list As L2List(Of T)
			Private head As Boolean

			Friend Sub New(list As L2List(Of T))
				Me.list = list
				Reset()
			End Sub

			Public Sub Reset() Implements IEnumerator.Reset
				curr = Nothing
				counter = list.updateCounter
				head = True
			End Sub

			Public ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					If curr Is Nothing OrElse counter <> list.updateCounter Then
						Throw New InvalidOperationException()
					End If
					Return curr
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
				If counter <> list.updateCounter Then
					Throw New InvalidOperationException()
				End If

				If head Then
					curr = list.head
					head = False
				ElseIf curr IsNot Nothing Then
					curr = curr.[next]
				End If

				Return curr IsNot Nothing
			End Function
		End Class

		Public Overrides Function GetEnumerator() As IEnumerator(Of T)
			Return New L2ListEnumerator(Me)
		End Function
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
