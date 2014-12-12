Imports System.Diagnostics
Namespace Volante

	''' <summary>
	''' Rectangle with integer coordinates. This class is used in spatial index.
	''' </summary>
	Public Structure Rectangle
		Private m_top As Integer
		Private m_left As Integer
		Private m_bottom As Integer
		Private m_right As Integer

		''' <summary>
		''' Smallest Y coordinate of the rectangle
		''' </summary>
		Public ReadOnly Property Top() As Integer
			Get
				Return m_top
			End Get
		End Property

		''' <summary>
		''' Smallest X coordinate of the rectangle
		''' </summary>
		Public ReadOnly Property Left() As Integer
			Get
				Return m_left
			End Get
		End Property

		''' <summary>
		''' Greatest Y coordinate  of the rectangle
		''' </summary>
		Public ReadOnly Property Bottom() As Integer
			Get
				Return m_bottom
			End Get
		End Property

		''' <summary>
		''' Greatest X coordinate  of the rectangle
		''' </summary>
		Public ReadOnly Property Right() As Integer
			Get
				Return m_right
			End Get
		End Property

		''' <summary>
		''' Rectangle area
		''' </summary>
		Public Function Area() As Long
			Return CLng(m_bottom - m_top) * (m_right - m_left)
		End Function

		''' <summary>
		''' Area of covered rectangle for two sepcified rectangles
		''' </summary>
		Public Shared Function JoinArea(a As Rectangle, b As Rectangle) As Long
			Dim left As Integer = If((a.left < b.left), a.left, b.left)
			Dim right As Integer = If((a.right > b.right), a.right, b.right)
			Dim top As Integer = If((a.top < b.top), a.top, b.top)
			Dim bottom As Integer = If((a.bottom > b.bottom), a.bottom, b.bottom)
			Return CLng(bottom - top) * (right - left)
		End Function

		''' <summary>
		''' Create copy of the rectangle
		''' </summary>
		Public Sub New(r As Rectangle)
			Me.m_top = r.top
			Me.m_left = r.left
			Me.m_bottom = r.bottom
			Me.m_right = r.right
		End Sub

		''' <summary>
		''' Construct rectangle with specified coordinates
		''' </summary>
		Public Sub New(top As Integer, left As Integer, bottom As Integer, right As Integer)
			Debug.Assert(top <= bottom AndAlso left <= right)
			Me.m_top = top
			Me.m_left = left
			Me.m_bottom = bottom
			Me.m_right = right
		End Sub

		''' <summary>
		''' Join two rectangles. This rectangle is updates to contain cover of this and specified rectangle.
		''' </summary>
		''' <param name="r">rectangle to be joined with this rectangle
		''' </param>
		Public Sub Join(r As Rectangle)
			If m_left > r.left Then
				m_left = r.left
			End If

			If m_right < r.right Then
				m_right = r.right
			End If

			If m_top > r.top Then
				m_top = r.top
			End If

			If m_bottom < r.bottom Then
				m_bottom = r.bottom
			End If
		End Sub

		''' <summary>
		'''  Non destructive join of two rectangles. 
		''' </summary>
		''' <param name="a">first joined rectangle
		''' </param>
		''' <param name="b">second joined rectangle
		''' </param>
		''' <returns>rectangle containing cover of these two rectangles
		''' </returns>
		Public Shared Function Join(a As Rectangle, b As Rectangle) As Rectangle
			Dim r As New Rectangle(a)
			r.Join(b)
			Return r
		End Function

		''' <summary>
		''' Checks if this rectangle intersects with specified rectangle
		''' </summary>
		Public Function Intersects(r As Rectangle) As Boolean
			Return m_left <= r.right AndAlso m_top <= r.bottom AndAlso m_right >= r.left AndAlso m_bottom >= r.top
		End Function

		''' <summary>
		''' Checks if this rectangle contains the specified rectangle
		''' </summary>
		Public Function Contains(r As Rectangle) As Boolean
			Return m_left <= r.left AndAlso m_top <= r.top AndAlso m_right >= r.right AndAlso m_bottom >= r.bottom
		End Function

		''' <summary>
		''' Check if rectanlge is empty 
		''' </summary>
		Public Function IsEmpty() As Boolean
			Return m_left > m_right
		End Function

		Public Function EqualsTo(other As Rectangle) As Boolean
			If Left <> other.Left Then
				Return False
			End If
			If Right <> other.Right Then
				Return False
			End If
			If Top <> other.Top Then
				Return False
			End If
			If Bottom <> other.Bottom Then
				Return False
			End If
			Return True
		End Function
	End Structure
End Namespace
