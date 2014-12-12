Imports System.Diagnostics
Namespace Volante

	''' <summary>
	''' R2 rectangle class. This class is used in spatial index.
	''' </summary>
	Public Structure RectangleR2
		Private m_top As Double
		Private m_left As Double
		Private m_bottom As Double
		Private m_right As Double

		''' <summary>
		''' Smallest Y coordinate of the rectangle
		''' </summary>
		Public ReadOnly Property Top() As Double
			Get
				Return m_top
			End Get
		End Property

		''' <summary>
		''' Smallest X coordinate of the rectangle
		''' </summary>
		Public ReadOnly Property Left() As Double
			Get
				Return m_left
			End Get
		End Property

		''' <summary>
		''' Greatest Y coordinate  of the rectangle
		''' </summary>
		Public ReadOnly Property Bottom() As Double
			Get
				Return m_bottom
			End Get
		End Property

		''' <summary>
		''' Greatest X coordinate  of the rectangle
		''' </summary>
		Public ReadOnly Property Right() As Double
			Get
				Return m_right
			End Get
		End Property

		''' <summary>
		''' Rectangle area
		''' </summary>
		Public Function Area() As Double
			Return (m_bottom - m_top) * (m_right - m_left)
		End Function

		''' <summary>
		''' Area of covered rectangle for two sepcified rectangles
		''' </summary>
		Public Shared Function JoinArea(a As RectangleR2, b As RectangleR2) As Double
			Dim left As Double = If((a.left < b.left), a.left, b.left)
			Dim right As Double = If((a.right > b.right), a.right, b.right)
			Dim top As Double = If((a.top < b.top), a.top, b.top)
			Dim bottom As Double = If((a.bottom > b.bottom), a.bottom, b.bottom)
			Return (bottom - top) * (right - left)
		End Function

		''' <summary>
		''' Create copy of the rectangle
		''' </summary>
		Public Sub New(r As RectangleR2)
			Me.m_top = r.top
			Me.m_left = r.left
			Me.m_bottom = r.bottom
			Me.m_right = r.right
		End Sub

		''' <summary>
		''' Construct rectangle with specified coordinates
		''' </summary>
		Public Sub New(top As Double, left As Double, bottom As Double, right As Double)
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
		Public Sub Join(r As RectangleR2)
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
		Public Shared Function Join(a As RectangleR2, b As RectangleR2) As RectangleR2
			Dim r As New RectangleR2(a)
			r.Join(b)
			Return r
		End Function

		''' <summary>
		''' Checks if this rectangle intersects with specified rectangle
		''' </summary>
		Public Function Intersects(r As RectangleR2) As Boolean
			Return m_left <= r.right AndAlso m_top <= r.bottom AndAlso m_right >= r.left AndAlso m_bottom >= r.top
		End Function

		''' <summary>
		''' Checks if this rectangle contains the specified rectangle
		''' </summary>
		Public Function Contains(r As RectangleR2) As Boolean
			Return m_left <= r.left AndAlso m_top <= r.top AndAlso m_right >= r.right AndAlso m_bottom >= r.bottom
		End Function

		''' <summary>
		''' Check if rectanlge is empty 
		''' </summary>
		Public Function IsEmpty() As Boolean
			Return m_left > m_right
		End Function

		Public Function EqualsTo(other As RectangleR2) As Boolean
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
