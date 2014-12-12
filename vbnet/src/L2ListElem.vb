Namespace Volante

	''' <summary>
	''' Double linked list element.
	''' </summary>
	Public Class L2ListElem(Of T As L2ListElem(Of T))
		Inherits Persistent
		Friend m_next As T
		Friend m_prev As T
		''' <summary>
		''' Get next list element. 
		''' Been called for the last list element, this method will return first element of the list 
		''' or list header
		''' </summary>
		Public ReadOnly Property [Next]() As T
			Get
				Return m_next
			End Get
		End Property

		''' <summary>
		''' Get previous list element. 
		''' Been call for the first list element, this method will return last element of the list 
		''' or list header
		''' </summary>
		Public ReadOnly Property Prev() As T
			Get
				Return m_prev
			End Get
		End Property
	End Class
End Namespace
